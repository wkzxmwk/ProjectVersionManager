using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Services;

public sealed class ProjectOptionCatalog(PvmDbContext db)
{
    public async Task EnsureValidAsync(
        IReadOnlyList<SaveProjectVersionRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var options = await db.ProjectOptions.AsNoTracking().ToListAsync(cancellationToken);
        var personnelNames = await db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER" && x.IsEnabled)
            .Select(x => (x.DisplayName ?? x.Username).Trim())
            .ToListAsync(cancellationToken);
        var personnelSet = personnelNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var available = options
            .Where(x => x.OptionType != ProjectOptionTypes.Developer && x.OptionType != ProjectOptionTypes.Compiler)
            .Select(x => $"{x.OptionType}\u001f{x.OptionValue.Trim()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var required = RequiredValues(request);
            var invalid = required
                .Where(item =>
                {
                    if (item.Key == ProjectOptionTypes.Developer || item.Key == ProjectOptionTypes.Compiler)
                    {
                        return !personnelSet.Contains(item.Value);
                    }
                    return !available.Contains($"{item.Key}\u001f{item.Value}");
                })
                .Select(item => DisplayName(item.Key))
                .ToArray();
            if (invalid.Length > 0)
            {
                errors.Add($"第 {index + 2} 行的{string.Join("、", invalid)}不在有效范围内（开发人/编译人需来自人员管理清单，其他需在项目设置中维护）");
            }
            else if (!ProgramBelongsToGroup(options, request.ProgramName, request.ProgramGroup, request.PersonnelGroup))
            {
                errors.Add($"第 {index + 2} 行的程序名不属于所选项目组或人员组");
            }
        }

        if (errors.Count > 0)
        {
            var detail = string.Join("；", errors.Take(20));
            if (errors.Count > 20)
            {
                detail += $"；另有 {errors.Count - 20} 行存在错误";
            }
            throw new RequestValidationException($"{detail}。");
        }
    }

    public async Task EnsureValidAsync(
        SaveProjectVersionRequest request,
        ProjectVersion? existing,
        CancellationToken cancellationToken)
    {
        var required = RequiredValues(request);

        var existingValues = ExistingValues(existing);
        var valuesToCheck = required
            .Where(item =>
                !existingValues.TryGetValue(item.Key, out var currentValue) ||
                !string.Equals(currentValue, item.Value, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        if (valuesToCheck.Count == 0)
        {
            return;
        }

        var validateProgramAssociation =
            valuesToCheck.ContainsKey(ProjectOptionTypes.ProgramGroup) ||
            valuesToCheck.ContainsKey(ProjectOptionTypes.ProgramName);
        var optionTypes = valuesToCheck.Keys
            .Where(k => k != ProjectOptionTypes.Developer && k != ProjectOptionTypes.Compiler)
            .ToList();
        if (validateProgramAssociation)
        {
            optionTypes.Add(ProjectOptionTypes.ProgramName);
        }
        optionTypes = optionTypes.Distinct(StringComparer.Ordinal).ToList();
        var options = await db.ProjectOptions.AsNoTracking()
            .Where(x => optionTypes.Contains(x.OptionType))
            .ToListAsync(cancellationToken);
        var available = options
            .Select(x => $"{x.OptionType}\u001f{x.OptionValue}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var personnelSet = (valuesToCheck.ContainsKey(ProjectOptionTypes.Developer) || valuesToCheck.ContainsKey(ProjectOptionTypes.Compiler))
            ? (await db.Users.AsNoTracking()
                .Where(x => x.RoleCode == "USER" && x.IsEnabled)
                .Select(x => (x.DisplayName ?? x.Username).Trim())
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var invalid = valuesToCheck
            .Where(item =>
            {
                if (item.Key == ProjectOptionTypes.Developer || item.Key == ProjectOptionTypes.Compiler)
                {
                    return !personnelSet!.Contains(item.Value);
                }
                return !available.Contains($"{item.Key}\u001f{item.Value}");
            })
            .Select(item => DisplayName(item.Key))
            .ToArray();

        if (invalid.Length > 0)
        {
            var personnelRelated = invalid.Any(x => x == "开发人" || x == "编译人");
            var msg = personnelRelated
                ? $"{string.Join("、", invalid)}不在有效范围内（开发人/编译人需来自人员管理清单，其他需在项目设置中维护），请刷新后重试。"
                : $"{string.Join("、", invalid)}不在项目设置的可选项中，请刷新选项后重试。";
            throw new RequestValidationException(msg);
        }
        if (validateProgramAssociation &&
            !ProgramBelongsToGroup(options, request.ProgramName, request.ProgramGroup, request.PersonnelGroup))
        {
            throw new RequestValidationException("所选程序名不属于当前项目组或人员组，请重新选择程序名。");
        }
    }

    private static Dictionary<string, string> RequiredValues(SaveProjectVersionRequest request)
    {
        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectOptionTypes.ProjectType] = request.ProjectType.Trim(),
            [ProjectOptionTypes.Developer] = request.Developer.Trim(),
            [ProjectOptionTypes.ProgramGroup] = request.ProgramGroup.Trim(),
            [ProjectOptionTypes.ProgramName] = request.ProgramName.Trim()
        };
        if (!string.IsNullOrWhiteSpace(request.Compiler))
        {
            required[ProjectOptionTypes.Compiler] = request.Compiler.Trim();
        }
        return required;
    }

    private static Dictionary<string, string> ExistingValues(ProjectVersion? existing)
    {
        if (existing is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectOptionTypes.ProjectType] = existing.ProjectType,
            [ProjectOptionTypes.Developer] = existing.Developer,
            [ProjectOptionTypes.ProgramGroup] = existing.ProgramGroup,
            [ProjectOptionTypes.ProgramName] = existing.ProgramName
        };
        if (!string.IsNullOrWhiteSpace(existing.Compiler))
        {
            values[ProjectOptionTypes.Compiler] = existing.Compiler;
        }
        return values;
    }

    private static bool ProgramBelongsToGroup(
        IEnumerable<ProjectOption> options,
        string programName,
        string programGroup,
        string? personnelGroup = null)
    {
        var trimmedProgramName = programName.Trim();
        var trimmedProgramGroup = programGroup.Trim();
        var hasPersonnelGroup = !string.IsNullOrWhiteSpace(personnelGroup);
        var trimmedPersonnelGroup = hasPersonnelGroup ? personnelGroup!.Trim() : null;

        return options.Any(option =>
            option.OptionType == ProjectOptionTypes.ProgramName &&
            string.Equals(option.OptionValue, trimmedProgramName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(option.ProgramGroup, trimmedProgramGroup, StringComparison.OrdinalIgnoreCase) &&
            (!hasPersonnelGroup ||
             string.IsNullOrEmpty(option.PersonnelGroup) ||
             string.Equals(option.PersonnelGroup, trimmedPersonnelGroup, StringComparison.OrdinalIgnoreCase)));
    }

    private static string DisplayName(string optionType) => optionType switch
    {
        ProjectOptionTypes.ProjectType => "项目类型",
        ProjectOptionTypes.StaffGroup => "项目组",
        ProjectOptionTypes.PersonnelGroup => "人员组",
        ProjectOptionTypes.Developer => "开发人",
        ProjectOptionTypes.ProgramGroup => "专案名称",
        ProjectOptionTypes.ProgramName => "程序名",
        ProjectOptionTypes.Compiler => "编译人",
        _ => optionType
    };
}
