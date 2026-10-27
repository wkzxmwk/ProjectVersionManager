using ClosedXML.Excel;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Services;

public static class ProjectOptionImportParser
{
    public const int MaximumRows = 1000;

    public static IReadOnlyList<ProjectOptionImportRow> Parse(IXLWorksheet sheet)
    {
        EnsureHeaders(sheet);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumRows)
        {
            throw new RequestValidationException($"单次最多导入 {MaximumRows} 行数据。");
        }

        var rows = new List<ProjectOptionImportRow>();
        var errors = new List<string>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var typeText = sheet.Cell(rowNumber, 1).GetString().Trim();
            var value = sheet.Cell(rowNumber, 2).GetString().Trim();
            var sortText = sheet.Cell(rowNumber, 3).GetFormattedString().Trim();
            var programGroup = sheet.Cell(rowNumber, 4).GetString().Trim();
            var staffGroup = sheet.Cell(rowNumber, 5).GetString().Trim();
            var personnelGroup = sheet.Cell(rowNumber, 6).GetString().Trim();
            if (string.IsNullOrWhiteSpace(typeText) &&
                string.IsNullOrWhiteSpace(value) &&
                string.IsNullOrWhiteSpace(sortText) &&
                string.IsNullOrWhiteSpace(programGroup) &&
                string.IsNullOrWhiteSpace(staffGroup) &&
                string.IsNullOrWhiteSpace(personnelGroup))
            {
                continue;
            }

            var optionType = NormalizeOptionType(typeText);
            if (optionType is null)
            {
                errors.Add($"第 {rowNumber} 行的选项分类“{typeText}”无效");
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"第 {rowNumber} 行的选项内容不能为空");
            }
            else if (value.Length > 200)
            {
                errors.Add($"第 {rowNumber} 行的选项内容不能超过 200 个字符");
            }

            var sortOrder = (rowNumber - 1) * 10;
            if (!string.IsNullOrWhiteSpace(sortText) &&
                (!int.TryParse(sortText, out sortOrder) || sortOrder is < 0 or > 999999))
            {
                errors.Add($"第 {rowNumber} 行的排序必须是 0 到 999999 之间的整数");
            }

            if (optionType is not null &&
                !string.IsNullOrWhiteSpace(value) &&
                value.Length <= 200 &&
                sortOrder is >= 0 and <= 999999)
            {
                rows.Add(new ProjectOptionImportRow(
                    optionType,
                    value,
                    sortOrder,
                    string.IsNullOrWhiteSpace(programGroup) ? null : programGroup,
                    string.IsNullOrWhiteSpace(staffGroup) ? null : staffGroup,
                    string.IsNullOrWhiteSpace(personnelGroup) ? null : personnelGroup));
            }
        }

        if (errors.Count > 0)
        {
            var detail = string.Join("；", errors.Take(20));
            if (errors.Count > 20)
            {
                detail += $"；另有 {errors.Count - 20} 个错误";
            }
            throw new RequestValidationException(detail);
        }
        if (rows.Count == 0)
        {
            throw new RequestValidationException("Excel 中没有可导入的选项数据。");
        }
        return rows;
    }

    private static void EnsureHeaders(IXLWorksheet sheet)
    {
        var actualHeaders = Enumerable.Range(1, 3)
            .Select(column => sheet.Cell(1, column).GetString().Trim())
            .ToArray();
        var expectedHeaders = new[] { "选项分类", "选项内容", "排序" };
        if (!actualHeaders.SequenceEqual(expectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Excel 第一行必须依次为：选项分类、选项内容、排序。");
        }

        var groupHeader = sheet.Cell(1, 4).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(groupHeader) &&
            !string.Equals(groupHeader, "所属专案", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Excel 第四列标题必须为：所属专案。");
        }

        var staffGroupHeader = sheet.Cell(1, 5).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(staffGroupHeader) &&
            !string.Equals(staffGroupHeader, "所属项目组", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Excel 第五列标题必须为：所属项目组。");
        }

        var personnelGroupHeader = sheet.Cell(1, 6).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(personnelGroupHeader) &&
            !string.Equals(personnelGroupHeader, "所属人员组", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Excel 第六列标题必须为：所属人员组。");
        }
    }

    private static string? NormalizeOptionType(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "项目类型" or ProjectOptionTypes.ProjectType => ProjectOptionTypes.ProjectType,
            "开发人" or ProjectOptionTypes.Developer => ProjectOptionTypes.Developer,
            "项目组" or ProjectOptionTypes.StaffGroup => ProjectOptionTypes.StaffGroup,
            "人员组" or "项目组下人员组" or ProjectOptionTypes.PersonnelGroup => ProjectOptionTypes.PersonnelGroup,
            "专案名称" or "项目组名称" or "程序组名称" or ProjectOptionTypes.ProgramGroup => ProjectOptionTypes.ProgramGroup,
            "程序名" or ProjectOptionTypes.ProgramName => ProjectOptionTypes.ProgramName,
            "编译人" or ProjectOptionTypes.Compiler => ProjectOptionTypes.Compiler,
            _ => null
        };
}

public sealed record ProjectOptionImportRow(
    string OptionType,
    string OptionValue,
    int SortOrder,
    string? ProgramGroup = null,
    string? StaffGroup = null,
    string? PersonnelGroup = null);
