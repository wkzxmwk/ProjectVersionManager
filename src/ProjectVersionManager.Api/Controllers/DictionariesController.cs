using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Controllers;

[ApiController]
[Authorize(Policy = SystemPermissions.VersionView)]
[Route("dictionaries")]
public sealed class DictionariesController(PvmDbContext db) : ControllerBase
{
    [HttpGet("project-types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetProjectTypes(
        CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(
            await GetValuesAsync(ProjectOptionTypes.ProjectType, cancellationToken)));
    }

    [HttpGet("developers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetDevelopers(
        CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(
            await GetPersonnelNamesAsync(cancellationToken)));
    }

    [HttpGet("staff-groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetStaffGroups(
        CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(
            await GetValuesAsync(ProjectOptionTypes.StaffGroup, cancellationToken)));
    }

    [HttpGet("personnel-groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetPersonnelGroups(
        [FromQuery] string? staffGroup,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup);

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = query.Where(x => x.StaffGroup == sg);
            }
            else
            {
                return Ok(ApiResponse<IReadOnlyList<string>>.Ok([]));
            }
        }
        else if (!string.IsNullOrWhiteSpace(staffGroup))
        {
            query = query.Where(x => x.StaffGroup == staffGroup.Trim());
        }

        var values = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(values));
    }

    [HttpGet("program-groups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetProgramGroups(
        [FromQuery] string? personnelGroup,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup);

        if (!string.IsNullOrWhiteSpace(personnelGroup))
        {
            var pg = personnelGroup.Trim();
            query = query.Where(x => x.PersonnelGroup == pg);
        }

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = query.Where(x => x.StaffGroup == sg);
            }
            else
            {
                return Ok(ApiResponse<IReadOnlyList<string>>.Ok([]));
            }
        }

        var values = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(values));
    }

    [HttpGet("program-group-options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProgramGroupOptionDto>>>> GetProgramGroupOptions(
        [FromQuery] string? personnelGroup,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup);

        if (!string.IsNullOrWhiteSpace(personnelGroup))
        {
            var pg = personnelGroup.Trim();
            query = query.Where(x => x.PersonnelGroup == pg);
        }

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = query.Where(x => x.StaffGroup == sg);
            }
            else
            {
                return Ok(ApiResponse<IReadOnlyList<ProgramGroupOptionDto>>.Ok([]));
            }
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .Select(x => new ProgramGroupOptionDto(
                x.OptionValue,
                x.PersonnelGroup ?? string.Empty,
                x.StaffGroup ?? string.Empty))
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProgramGroupOptionDto>>.Ok(items));
    }

    [HttpGet("program-names")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetProgramNames(
        [FromQuery] string? programGroup,
        [FromQuery] string? personnelGroup,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramName);
        if (!string.IsNullOrWhiteSpace(programGroup))
        {
            var normalizedGroup = programGroup.Trim();
            query = query.Where(x => x.ProgramGroup == normalizedGroup);
        }
        if (!string.IsNullOrWhiteSpace(personnelGroup))
        {
            var pg = personnelGroup.Trim();
            query = query.Where(x => x.PersonnelGroup == pg);
        }

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = query.Where(x =>
                    (x.StaffGroup == null || x.StaffGroup == sg));
            }
        }

        var values = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(values));
    }

    [HttpGet("compilers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetCompilers(
        CancellationToken cancellationToken)
    {
        var values = await db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER" && x.IsEnabled)
            .OrderBy(x => x.DisplayName ?? x.Username)
            .Select(x => x.DisplayName ?? x.Username)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(values));
    }

    private async Task<IReadOnlyList<string>> GetValuesAsync(
        string optionType,
        CancellationToken cancellationToken)
    {
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == optionType);

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = optionType switch
                {
                    ProjectOptionTypes.Developer or ProjectOptionTypes.Compiler or ProjectOptionTypes.ProgramGroup or ProjectOptionTypes.PersonnelGroup =>
                        query.Where(x => x.StaffGroup == sg),
                    _ => query
                };
            }
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
    }

    private string? CurrentStaffGroup() =>
        User.FindFirstValue("staff_group");

    private bool IsAdmin() =>
        string.Equals(User.FindFirstValue(ClaimTypes.Role), "ADMIN", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<string>> GetPersonnelNamesAsync(CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER" && x.IsEnabled);

        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (!string.IsNullOrEmpty(sg))
            {
                query = query.Where(x => x.StaffGroup == sg);
            }
            else
            {
                return [];
            }
        }

        return await query
            .OrderBy(x => x.DisplayName ?? x.Username)
            .Select(x => x.DisplayName ?? x.Username)
            .ToListAsync(cancellationToken);
    }
}
