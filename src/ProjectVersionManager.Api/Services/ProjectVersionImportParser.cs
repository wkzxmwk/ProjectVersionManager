using System.Globalization;
using ClosedXML.Excel;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Services;

public static class ProjectVersionImportParser
{
    public const int MaximumRows = 1000;

    public static readonly string[] Headers =
    [
        "项目时间",
        "项目类型",
        "开发人",
        "专案名称",
        "人员组",
        "程序名",
        "程序版本",
        "程序版本描述",
        "编译人",
        "编译时间",
        "编译程序版本",
        "释放时间",
        "关联分支名",
        "备注1",
        "备注2",
        "状态"
    ];

    public static IReadOnlyList<SaveProjectVersionRequest> Parse(IXLWorksheet sheet)
    {
        EnsureHeaders(sheet);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumRows)
        {
            throw new RequestValidationException($"单次最多导入 {MaximumRows} 行数据。");
        }

        var rows = new List<SaveProjectVersionRequest>();
        var errors = new List<string>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            if (Enumerable.Range(1, Headers.Length)
                .All(column => string.IsNullOrWhiteSpace(sheet.Cell(rowNumber, column).GetFormattedString())))
            {
                continue;
            }

            var projectDate = ReadRequiredDate(sheet.Cell(rowNumber, 1), rowNumber, "项目时间", errors);
            var projectType = ReadRequiredText(sheet.Cell(rowNumber, 2), rowNumber, "项目类型", 50, errors);
            var developer = ReadRequiredText(sheet.Cell(rowNumber, 3), rowNumber, "开发人", 100, errors);
            var programGroup = ReadRequiredText(sheet.Cell(rowNumber, 4), rowNumber, "专案名称", 100, errors);
            var personnelGroup = ReadOptionalText(sheet.Cell(rowNumber, 5), rowNumber, "人员组", 100, errors);
            var programName = ReadRequiredText(sheet.Cell(rowNumber, 6), rowNumber, "程序名", 200, errors);
            var programVersion = ReadRequiredText(sheet.Cell(rowNumber, 7), rowNumber, "程序版本", 50, errors);
            var versionDescription = ReadOptionalText(
                sheet.Cell(rowNumber, 8), rowNumber, "程序版本描述", 2000, errors);
            var compiler = ReadOptionalText(sheet.Cell(rowNumber, 9), rowNumber, "编译人", 100, errors);
            var compileTime = ReadOptionalDate(sheet.Cell(rowNumber, 10), rowNumber, "编译时间", errors);
            var compiledProgramVersion = ReadOptionalText(
                sheet.Cell(rowNumber, 11), rowNumber, "编译程序版本", 200, errors);
            var releaseTime = ReadOptionalDate(sheet.Cell(rowNumber, 12), rowNumber, "释放时间", errors);
            var branchName = ReadOptionalText(sheet.Cell(rowNumber, 13), rowNumber, "关联分支名", 300, errors);
            var remark1 = ReadOptionalText(sheet.Cell(rowNumber, 14), rowNumber, "备注1", 2000, errors);
            var remark2 = ReadOptionalText(sheet.Cell(rowNumber, 15), rowNumber, "备注2", 2000, errors);
            var status = NormalizeStatus(sheet.Cell(rowNumber, 16).GetString());
            if (status is null)
            {
                errors.Add($"第 {rowNumber} 行的状态无效，可填写待释放、待编译、已编译、提前释放、已释放、已取消、已注释或对应英文代码");
            }

            if (projectDate.HasValue &&
                projectType is not null &&
                developer is not null &&
                programGroup is not null &&
                programName is not null &&
                programVersion is not null &&
                status is not null)
            {
                rows.Add(new SaveProjectVersionRequest
                {
                    ProjectDate = projectDate.Value,
                    ProjectType = projectType,
                    Developer = developer,
                    ProgramGroup = programGroup,
                    PersonnelGroup = personnelGroup,
                    ProgramName = programName,
                    ProgramVersion = programVersion,
                    Compiler = compiler,
                    CompileTime = compileTime,
                    CompiledProgramVersion = compiledProgramVersion,
                    VersionDescription = versionDescription,
                    ReleaseTime = releaseTime,
                    BranchName = branchName,
                    Remark1 = remark1,
                    Remark2 = remark2,
                    Status = status
                });
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
            throw new RequestValidationException("Excel 中没有可导入的项目版本数据。");
        }
        return rows;
    }

    private static void EnsureHeaders(IXLWorksheet sheet)
    {
        var actualHeaders = Enumerable.Range(1, Headers.Length)
            .Select(column => sheet.Cell(1, column).GetString().Trim())
            .ToArray();
        if (!actualHeaders.SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Excel 第一行必须依次为：{string.Join("、", Headers)}。请使用系统提供的导入模板。");
        }
    }

    private static string? ReadRequiredText(
        IXLCell cell,
        int rowNumber,
        string fieldName,
        int maximumLength,
        ICollection<string> errors)
    {
        var value = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"第 {rowNumber} 行的{fieldName}不能为空");
            return null;
        }
        ValidateLength(value, rowNumber, fieldName, maximumLength, errors);
        return value.Length <= maximumLength ? value : null;
    }

    private static string? ReadOptionalText(
        IXLCell cell,
        int rowNumber,
        string fieldName,
        int maximumLength,
        ICollection<string> errors)
    {
        var value = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        ValidateLength(value, rowNumber, fieldName, maximumLength, errors);
        return value.Length <= maximumLength ? value : null;
    }

    private static void ValidateLength(
        string value,
        int rowNumber,
        string fieldName,
        int maximumLength,
        ICollection<string> errors)
    {
        if (value.Length > maximumLength)
        {
            errors.Add($"第 {rowNumber} 行的{fieldName}不能超过 {maximumLength} 个字符");
        }
    }

    private static DateTime? ReadRequiredDate(
        IXLCell cell,
        int rowNumber,
        string fieldName,
        ICollection<string> errors)
    {
        var value = ReadDate(cell);
        if (!value.HasValue)
        {
            errors.Add($"第 {rowNumber} 行的{fieldName}不能为空且必须是有效日期");
        }
        return value;
    }

    private static DateTime? ReadOptionalDate(
        IXLCell cell,
        int rowNumber,
        string fieldName,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(cell.GetFormattedString()))
        {
            return null;
        }
        var value = ReadDate(cell);
        if (!value.HasValue)
        {
            errors.Add($"第 {rowNumber} 行的{fieldName}不是有效日期时间");
        }
        return value;
    }

    private static DateTime? ReadDate(IXLCell cell)
    {
        if (cell.TryGetValue<DateTime>(out var date))
        {
            return date;
        }

        var text = cell.GetFormattedString().Trim();
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy/M/d",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/M/d H:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy/M/d H:mm"
        };
        return DateTime.TryParseExact(
            text,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out date)
            ? date
            : null;
    }

    private static string? NormalizeStatus(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "待释放" or "PENDING" => "PENDING",
            "待编译" or "WAIT_COMPILE" => "WAIT_COMPILE",
            "已编译" or "COMPILED" => "COMPILED",
            "提前释放" or "PRE_RELEASED" => "PRE_RELEASED",
            "已释放" or "RELEASED" => "RELEASED",
            "已取消" or "CANCELED" => "CANCELED",
            "已注释" or "COMMENTED" => "COMMENTED",
            _ => null
        };
}
