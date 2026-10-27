using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Controllers;

[ApiController]
[Authorize(Policy = SystemPermissions.PersonnelManage)]
[Route("personnel")]
public sealed class PersonnelController(PvmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PersonnelDto>>>> GetList(
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER")
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);
        var userIds = users.Select(x => x.Id).ToList();
        var permissions = await db.UserPermissions.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PersonnelDto>>.Ok(
            users.Select(user => ToDto(
                user,
                permissions
                    .Where(permission => permission.UserId == user.Id)
                    .Select(permission => permission.PermissionCode)
                    .OrderBy(code => code)
                    .ToList())).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PersonnelDto>>> Create(
        [FromBody] CreatePersonnelRequest request,
        CancellationToken cancellationToken)
    {
        var employeeNumber = NormalizeEmployeeNumber(request.EmployeeNumber);
        var personnelName = NormalizePersonnelName(request.PersonnelName);
        await EnsureUniqueAsync(employeeNumber, personnelName, null, cancellationToken);
        EnforceStaffGroupScope(request.StaffGroup);
        if (string.IsNullOrWhiteSpace(request.PersonnelGroup))
        {
            throw new RequestValidationException("所属人员组不能为空，请先在项目设置中维护人员组。");
        }

        var now = DateTime.Now;
        var entity = new PvmUser
        {
            Id = Guid.NewGuid().ToString(),
            Username = employeeNumber,
            DisplayName = personnelName,
            RoleCode = "USER",
            StaffGroup = request.StaffGroup,
            PersonnelGroup = request.PersonnelGroup,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        entity.PasswordHash = new PasswordHasher<PvmUser>()
            .HashPassword(entity, request.ManagementPassword);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Users.Add(entity);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetList),
            null,
            ApiResponse<PersonnelDto>.Ok(ToDto(entity, []), "操作人员创建成功"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<PersonnelDto>>> Update(
        string id,
        [FromBody] UpdatePersonnelRequest request,
        CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Id == id && x.RoleCode == "USER")
            .ToListAsync(cancellationToken);
        var entity = users.SingleOrDefault()
            ?? throw new NotFoundException("操作人员不存在。");
        var employeeNumber = NormalizeEmployeeNumber(request.EmployeeNumber);
        var personnelName = NormalizePersonnelName(request.PersonnelName);
        await EnsureUniqueAsync(employeeNumber, personnelName, id, cancellationToken);

        var oldPersonnelName = entity.DisplayName ?? entity.Username;
        var now = DateTime.Now;
        EnforceStaffGroupScope(request.StaffGroup);
        if (string.IsNullOrWhiteSpace(request.PersonnelGroup))
        {
            throw new RequestValidationException("所属人员组不能为空，请先在项目设置中维护人员组。");
        }
        entity.Username = employeeNumber;
        entity.DisplayName = personnelName;
        entity.StaffGroup = request.StaffGroup;
        entity.PersonnelGroup = request.PersonnelGroup;
        entity.UpdatedAt = now;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var permissionCodes = await db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == entity.Id)
            .Select(x => x.PermissionCode)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<PersonnelDto>.Ok(
            ToDto(entity, permissionCodes),
            "操作人员信息更新成功"));
    }

    [HttpPut("{id}/permissions")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<PersonnelDto>>> UpdatePermissions(
        string id,
        [FromBody] UpdatePersonnelPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Id == id && x.RoleCode == "USER")
            .ToListAsync(cancellationToken);
        var entity = users.SingleOrDefault()
            ?? throw new NotFoundException("操作人员不存在。");
        var permissions = request.Permissions
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var invalid = permissions.Except(SystemPermissions.All, StringComparer.Ordinal).ToArray();
        if (invalid.Length > 0)
        {
            throw new RequestValidationException(
                $"包含不支持的权限代码：{string.Join("、", invalid)}。");
        }
        var versionWritePermissions = new[]
        {
            SystemPermissions.VersionCreate,
            SystemPermissions.VersionUpdate,
            SystemPermissions.VersionDelete,
            SystemPermissions.VersionImport
        };
        if (permissions.Any(versionWritePermissions.Contains) &&
            !permissions.Contains(SystemPermissions.VersionView))
        {
            permissions.Add(SystemPermissions.VersionView);
        }

        var existing = await db.UserPermissions
            .Where(x => x.UserId == id)
            .ToListAsync(cancellationToken);
        var now = DateTime.Now;
        var username = CurrentUsername();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.UserPermissions.RemoveRange(existing);
        db.UserPermissions.AddRange(permissions.Select(permission => new PvmUserPermission
        {
            Id = Guid.NewGuid().ToString(),
            UserId = entity.Id,
            PermissionCode = permission,
            CreatedBy = username,
            CreatedAt = now
        }));
        entity.UpdatedAt = now;
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<PersonnelDto>.Ok(
            ToDto(entity, permissions),
            "人员权限更新成功，用户重新登录后生效"));
    }

    [HttpPost("{id}/reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        string id,
        [FromBody] ResetPersonnelPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Id == id && x.RoleCode == "USER")
            .ToListAsync(cancellationToken);
        var entity = users.SingleOrDefault()
            ?? throw new NotFoundException("操作人员不存在。");
        EnforceStaffGroupScope(entity.StaffGroup);
        entity.PasswordHash = new PasswordHasher<PvmUser>()
            .HashPassword(entity, request.ManagementPassword);
        entity.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "管理密码重置成功"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Id == id && x.RoleCode == "USER")
            .ToListAsync(cancellationToken);
        var entity = users.SingleOrDefault()
            ?? throw new NotFoundException("操作人员不存在。");
        EnforceStaffGroupScope(entity.StaffGroup);
        var personnelName = entity.DisplayName ?? entity.Username;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var permissions = await db.UserPermissions
            .Where(x => x.UserId == entity.Id)
            .ToListAsync(cancellationToken);
        db.UserPermissions.RemoveRange(permissions);
        db.Users.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "操作人员已删除"));
    }

    private async Task EnsureUniqueAsync(
        string employeeNumber,
        string personnelName,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => excludedId == null || x.Id != excludedId)
            .ToListAsync(cancellationToken);
        if (users.Any(x => string.Equals(
                x.Username.Trim(), employeeNumber, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("该操作人员工号已存在。");
        }
        if (users.Any(x => x.RoleCode == "USER" && string.Equals(
                (x.DisplayName ?? x.Username).Trim(),
                personnelName,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("该人员姓名已存在。");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("人员或关联选项数据已发生变化，请刷新后重试。");
        }
    }

    private static string NormalizeEmployeeNumber(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new RequestValidationException("操作人员工号不能为空。");
        }
        return normalized;
    }

    private static string NormalizePersonnelName(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new RequestValidationException("人员姓名不能为空。");
        }
        return normalized;
    }

    private string CurrentUsername() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    private string? CurrentStaffGroup() =>
        User.FindFirstValue("staff_group");

    private bool IsAdmin() =>
        User.FindFirstValue(ClaimTypes.Role)?.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) == true;

    private void EnforceStaffGroupScope(string? targetStaffGroup)
    {
        if (IsAdmin()) return;
        var currentSg = CurrentStaffGroup();
        if (string.IsNullOrWhiteSpace(currentSg))
        {
            throw new ForbiddenException("当前账号未分配项目组，无法维护操作人员");
        }
        if (!string.Equals(targetStaffGroup, currentSg, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("非管理员只能在所属项目组内维护操作人员");
        }
    }

    private static PersonnelDto ToDto(
        PvmUser entity,
        IReadOnlyList<string> permissions) =>
        new(
            entity.Id,
            entity.Username,
            entity.DisplayName ?? entity.Username,
            entity.StaffGroup,
            entity.PersonnelGroup,
            entity.CreatedAt,
            entity.UpdatedAt,
            permissions);
}
