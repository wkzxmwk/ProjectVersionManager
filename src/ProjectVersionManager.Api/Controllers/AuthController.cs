using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;
using ProjectVersionManager.Api.Services;

namespace ProjectVersionManager.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(PvmDbContext db, IJwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        // Materialize first to keep the query compatible with the local SQL Server setup.
        var matchingUsers = await db.Users
            .Where(x => x.Username == username && x.IsEnabled)
            .ToListAsync(cancellationToken);
        var user = matchingUsers.SingleOrDefault();

        if (user is null)
        {
            return Unauthorized(new { message = "用户名或密码错误。" });
        }

        PasswordVerificationResult result;
        try
        {
            result = new PasswordHasher<Entities.PvmUser>()
                .VerifyHashedPassword(user, user.PasswordHash, request.Password);
        }
        catch (FormatException)
        {
            return Unauthorized(new { message = "用户密码尚未转换为安全哈希，请联系管理员。" });
        }
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "用户名或密码错误。" });
        }

        var permissions = user.RoleCode == "ADMIN"
            ? SystemPermissions.All
            : await db.UserPermissions.AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Select(x => x.PermissionCode)
                .ToListAsync(cancellationToken);

        return Ok(ApiResponse<LoginResponse>.Ok(
            tokenService.Create(user, permissions),
            "登录成功"));
    }

    [Authorize]
    [HttpPost("logout")]
    public ActionResult<ApiResponse<object>> Logout() =>
        Ok(ApiResponse<object>.Ok(new { }, "已退出登录"));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            throw new RequestValidationException("新密码不能与当前密码相同。");
        }

        var username = User.Identity?.Name
            ?? throw new UnauthorizedException("当前登录信息无效，请重新登录。");
        var users = await db.Users
            .Where(x => x.Username == username && x.IsEnabled)
            .ToListAsync(cancellationToken);
        var user = users.SingleOrDefault()
            ?? throw new UnauthorizedException("当前登录用户不存在，请重新登录。");

        var hasher = new PasswordHasher<Entities.PvmUser>();
        PasswordVerificationResult verification;
        try
        {
            verification = hasher.VerifyHashedPassword(
                user, user.PasswordHash, request.CurrentPassword);
        }
        catch (FormatException)
        {
            throw new RequestValidationException("当前密码格式异常，请联系管理员重置密码。");
        }
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new RequestValidationException("当前密码不正确。");
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { user.Id }, "密码修改成功，请重新登录"));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<ApiResponse<object>> Me() =>
        Ok(ApiResponse<object>.Ok(new
        {
            username = User.Identity?.Name,
            displayName = User.FindFirst("display_name")?.Value ?? User.Identity?.Name,
            roleCode = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            staffGroup = User.FindFirst("staff_group")?.Value,
            personnelGroup = User.FindFirst("personnel_group")?.Value,
            permissions = User.FindAll(SystemPermissions.ClaimType).Select(x => x.Value).ToArray()
        }));
}
