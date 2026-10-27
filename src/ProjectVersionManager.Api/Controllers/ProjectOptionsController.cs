using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;
using ProjectVersionManager.Api.Services;

namespace ProjectVersionManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("project-options")]
public sealed class ProjectOptionsController(PvmDbContext db) : ControllerBase
{
    private const long MaximumImportFileSize = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectOptionDto>>>> GetList(
        [FromQuery] string? optionType,
        CancellationToken cancellationToken)
    {
        if (!CanAccessProjectSettings())
        {
            return Forbid();
        }
        if (!string.IsNullOrWhiteSpace(optionType) && !ProjectOptionTypes.All.Contains(optionType))
        {
            throw new RequestValidationException("不支持的选项类型。");
        }

        var query = db.ProjectOptions.AsNoTracking();
        if (!HasPermission(SystemPermissions.OptionManage))
        {
            query = query.Where(x =>
                x.OptionType == ProjectOptionTypes.ProgramGroup ||
                x.OptionType == ProjectOptionTypes.ProgramName);
            if (!string.IsNullOrWhiteSpace(optionType) && !CanManageOptionType(optionType))
            {
                return Forbid();
            }
        }
        if (!string.IsNullOrWhiteSpace(optionType))
        {
            query = query.Where(x => x.OptionType == optionType);
        }
        var entities = await query
            .OrderBy(x => x.OptionType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProjectOptionDto>>.Ok(
            entities.Select(ToDto).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProjectOptionDto>>> Create(
        [FromBody] SaveProjectOptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageOptionType(request.OptionType))
        {
            return Forbid();
        }
        if (request.OptionType == ProjectOptionTypes.Developer || request.OptionType == ProjectOptionTypes.Compiler)
        {
            throw new RequestValidationException("开发人和编译人选项由人员管理统一维护，请直接在人员管理中新增操作人员。");
        }
        var value = NormalizeValue(request.OptionValue);
        var staffGroup = await NormalizeStaffGroupAsync(
            request.OptionType, request.StaffGroup, cancellationToken);
        var personnelGroup = await NormalizePersonnelGroupAsync(
            request.OptionType, staffGroup, request.PersonnelGroup, cancellationToken);
        var programGroup = await NormalizeProgramGroupAsync(
            request.OptionType, request.ProgramGroup, cancellationToken);
        await EnsureUniqueAsync(request.OptionType, value, staffGroup, personnelGroup, null, cancellationToken);
        var now = DateTime.Now;
        var entity = new ProjectOption
        {
            Id = Guid.NewGuid().ToString(),
            OptionType = request.OptionType,
            OptionValue = value,
            StaffGroup = staffGroup,
            PersonnelGroup = personnelGroup,
            ProgramGroup = programGroup,
            SortOrder = request.SortOrder,
            CreatedBy = CurrentUsername(),
            CreatedAt = now,
            UpdatedBy = CurrentUsername(),
            UpdatedAt = now,
            RowVersion = 1
        };
        db.ProjectOptions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetList),
            new { optionType = entity.OptionType },
            ApiResponse<ProjectOptionDto>.Ok(ToDto(entity), "项目可选项创建成功"));
    }

    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadImportTemplate(
        [FromQuery] string? optionType,
        CancellationToken cancellationToken)
    {
        if (!CanAccessProjectSettings())
        {
            return Forbid();
        }

        var isProgramGroup = string.Equals(optionType, ProjectOptionTypes.ProgramGroup, StringComparison.OrdinalIgnoreCase);
        var sheetName = isProgramGroup ? "专案名称导入" : "项目选项导入";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "选项分类";
        sheet.Cell(1, 2).Value = "选项内容";
        sheet.Cell(1, 3).Value = "排序";
        sheet.Cell(1, 4).Value = "所属专案";
        sheet.Cell(1, 5).Value = "所属项目组";
        sheet.Cell(1, 6).Value = "所属人员组";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Font.FontColor = XLColor.White;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        sheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 1, 6).SetAutoFilter();
        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 28;
        sheet.Column(3).Width = 10;
        sheet.Column(4).Width = 22;
        sheet.Column(5).Width = 18;
        sheet.Column(6).Width = 18;

        var guide = workbook.Worksheets.Add("填写说明");
        guide.Cell("A1").Value = isProgramGroup ? "专案名称批量导入说明" : "项目选项批量导入说明";
        guide.Cell("A1").Style.Font.Bold = true;
        guide.Cell("A1").Style.Font.FontSize = 16;

        string[] instructions;
        if (isProgramGroup)
        {
            instructions = new[]
            {
                "1. 请勿修改「专案名称导入」工作表名称及第一行标题。",
                "2. 选项分类填写「专案名称」；选项内容填写专案名称（必填，不超过 200 字符）。",
                "3. 所属项目组：必填，须在「项目设置」中已存在或先于本批导入。",
                "4. 所属人员组：必填，须在「项目设置」中已存在或先于本批导入，且须属于对应项目组。",
                "5. 所属专案：专案名称不需要填写此列，留空即可。",
                "6. 排序留空时按行号自动生成，可填 0~999999 的整数。",
                $"7. 仅支持 .xlsx，文件不超过 5 MB，单次最多 {ProjectOptionImportParser.MaximumRows} 行。",
                "8. 任意一行格式错误都会阻止整批导入；与现有数据或文件内重复的选项会自动跳过。",
            };
        }
        else
        {
            instructions = new[]
            {
                "1. 请勿修改「项目选项导入」工作表名称及第一行标题。",
                "2. 选项分类可填写：项目类型、项目组、人员组、专案名称、程序名、开发人、编译人。",
                "3. 选项内容为必填（不超过 200 字符）；排序留空时按行号自动生成。",
                "4. 所属专案：仅程序名需填写（必填），其他分类留空。",
                "5. 所属项目组：开发人、编译人、人员组、专案名称、程序名可填写；项目类型留空。",
                "6. 所属人员组：仅专案名称和程序名可填写；专案名称为必填，程序名可选。",
                $"7. 仅支持 .xlsx，文件不超过 5 MB，单次最多 {ProjectOptionImportParser.MaximumRows} 行。",
                "8. 任意一行格式错误都会阻止整批导入；与现有数据或文件内重复的选项会自动跳过。",
            };
        }
        for (var index = 0; index < instructions.Length; index++)
        {
            guide.Cell(index + 3, 1).Value = instructions[index];
        }
        guide.Column(1).Width = 110;

        var refSheet = workbook.Worksheets.Add("可选项参考");
        var options = await db.ProjectOptions.AsNoTracking()
            .OrderBy(x => x.OptionType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .ToListAsync(cancellationToken);

        if (isProgramGroup)
        {
            refSheet.Cell(1, 1).Value = "项目组";
            refSheet.Cell(1, 2).Value = "人员组";
            refSheet.Cell(1, 3).Value = "人员组所属项目组";
            {
                var staffGroups = options
                    .Where(x => x.OptionType == ProjectOptionTypes.StaffGroup)
                    .Select(x => x.OptionValue)
                    .OrderBy(x => x)
                    .ToList();
                for (var row = 0; row < staffGroups.Count; row++)
                    refSheet.Cell(row + 2, 1).Value = staffGroups[row];
            }
            {
                var personnelGroups = options
                    .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup)
                    .OrderBy(x => x.StaffGroup)
                    .ThenBy(x => x.OptionValue)
                    .ToList();
                for (var row = 0; row < personnelGroups.Count; row++)
                {
                    refSheet.Cell(row + 2, 2).Value = personnelGroups[row].OptionValue;
                    refSheet.Cell(row + 2, 3).Value = personnelGroups[row].StaffGroup ?? "";
                }
            }
        }
        else
        {
            refSheet.Cell(1, 1).Value = "项目类型";
            {
                var types = options.Where(x => x.OptionType == ProjectOptionTypes.ProjectType).Select(x => x.OptionValue).ToList();
                for (var row = 0; row < types.Count; row++) refSheet.Cell(row + 2, 1).Value = types[row];
            }
            refSheet.Cell(1, 2).Value = "项目组";
            refSheet.Cell(1, 3).Value = "人员组";
            refSheet.Cell(1, 4).Value = "专案名称";
            refSheet.Cell(1, 5).Value = "程序名";
            {
                var staffGroups = options.Where(x => x.OptionType == ProjectOptionTypes.StaffGroup).Select(x => x.OptionValue).ToList();
                for (var row = 0; row < staffGroups.Count; row++) refSheet.Cell(row + 2, 2).Value = staffGroups[row];
            }
            {
                var personnelGroups = options.Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup).OrderBy(x => x.OptionValue).ToList();
                for (var row = 0; row < personnelGroups.Count; row++) refSheet.Cell(row + 2, 3).Value = personnelGroups[row].OptionValue;
            }
            {
                var programGroups = options.Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup).Select(x => x.OptionValue).ToList();
                for (var row = 0; row < programGroups.Count; row++) refSheet.Cell(row + 2, 4).Value = programGroups[row];
            }
            {
                var programNames = options.Where(x => x.OptionType == ProjectOptionTypes.ProgramName).Select(x => x.OptionValue).ToList();
                for (var row = 0; row < programNames.Count; row++) refSheet.Cell(row + 2, 5).Value = programNames[row];
            }
        }
        refSheet.Row(1).Style.Font.Bold = true;
        refSheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF0FF");
        refSheet.Columns().Width = 24;
        refSheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = isProgramGroup ? "专案名称批量导入模板.xlsx" : "项目选项批量导入模板.xlsx";
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost("import")]
    [RequestSizeLimit(MaximumImportFileSize)]
    public async Task<ActionResult<ApiResponse<ProjectOptionImportResultDto>>> Import(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new RequestValidationException("请选择需要导入的 Excel 文件。");
        }
        if (file.Length > MaximumImportFileSize)
        {
            throw new RequestValidationException("导入文件不能超过 5 MB。");
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("批量导入仅支持 .xlsx 文件。");
        }

        IReadOnlyList<ProjectOptionImportRow> importedRows;
        try
        {
            await using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new RequestValidationException("Excel 文件中没有可读取的工作表。");
            importedRows = ProjectOptionImportParser.Parse(sheet);
        }
        catch (RequestValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RequestValidationException($"无法读取 Excel 文件：{exception.Message}");
        }

        if (importedRows.Any(row => !CanManageOptionType(row.OptionType)))
        {
            return Forbid();
        }

        var existingOptions = await db.ProjectOptions.AsNoTracking()
            .Select(x => new { x.OptionType, x.OptionValue, x.ProgramGroup, x.StaffGroup })
            .ToListAsync(cancellationToken);
        var uniqueKeys = existingOptions
            .Select(x => ImportKey(x.OptionType, x.OptionValue))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var created = new List<ProjectOption>();
        var skippedDuplicates = 0;
        var now = DateTime.Now;
        var username = CurrentUsername();
        var availableGroups = existingOptions
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup)
            .Select(x => x.OptionValue)
            .Concat(importedRows
                .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup)
                .Select(x => x.OptionValue))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableStaffGroups = existingOptions
            .Where(x => x.OptionType == ProjectOptionTypes.StaffGroup)
            .Select(x => x.OptionValue)
            .Concat(importedRows
                .Where(x => x.OptionType == ProjectOptionTypes.StaffGroup)
                .Select(x => x.OptionValue))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availablePersonnelGroups = existingOptions
            .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup)
            .Select(x => (x.OptionValue, x.StaffGroup))
            .Concat(importedRows
                .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup)
                .Select(x => (OptionValue: x.OptionValue, StaffGroup: x.StaffGroup)))
            .ToList();
        foreach (var row in importedRows)
        {
            var programGroup = row.OptionType == ProjectOptionTypes.ProgramName
                ? NormalizeImportedProgramGroup(row.ProgramGroup, availableGroups)
                : NormalizeImportedNonProgramGroup(row.ProgramGroup);
            var staffGroup = (row.OptionType == ProjectOptionTypes.Developer ||
                              row.OptionType == ProjectOptionTypes.Compiler ||
                              row.OptionType == ProjectOptionTypes.PersonnelGroup ||
                              row.OptionType == ProjectOptionTypes.ProgramGroup ||
                              row.OptionType == ProjectOptionTypes.ProgramName)
                ? NormalizeImportedStaffGroup(row.StaffGroup, availableStaffGroups)
                : NormalizeImportedNonStaffGroup(row.StaffGroup);
            var personnelGroup = (row.OptionType == ProjectOptionTypes.ProgramGroup ||
                                  row.OptionType == ProjectOptionTypes.ProgramName)
                ? NormalizeImportedPersonnelGroup(row.PersonnelGroup, staffGroup, availablePersonnelGroups)
                : NormalizeImportedNonPersonnelGroup(row.PersonnelGroup);
            if (!uniqueKeys.Add(ImportKey(row.OptionType, row.OptionValue)))
            {
                skippedDuplicates++;
                continue;
            }

            created.Add(new ProjectOption
            {
                Id = Guid.NewGuid().ToString(),
                OptionType = row.OptionType,
                OptionValue = row.OptionValue,
                StaffGroup = staffGroup,
                PersonnelGroup = personnelGroup,
                ProgramGroup = programGroup,
                SortOrder = row.SortOrder,
                CreatedBy = username,
                CreatedAt = now,
                UpdatedBy = username,
                UpdatedAt = now,
                RowVersion = 1
            });
        }

        if (created.Count > 0)
        {
            db.ProjectOptions.AddRange(created);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new ConflictException("导入过程中选项数据已发生变化，请刷新设置后重新导入。");
            }
        }

        var result = new ProjectOptionImportResultDto(
            importedRows.Count,
            created.Count,
            skippedDuplicates);
        return Ok(ApiResponse<ProjectOptionImportResultDto>.Ok(
            result,
            $"成功导入 {created.Count} 个选项，跳过 {skippedDuplicates} 个重复项。"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProjectOptionDto>>> Update(
        string id,
        [FromBody] SaveProjectOptionRequest request,
        CancellationToken cancellationToken)
    {
        var entities = await db.ProjectOptions.Where(x => x.Id == id).ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目可选项不存在。");
        if (!CanManageOptionType(entity.OptionType) || !CanManageOptionType(request.OptionType))
        {
            return Forbid();
        }
        if (entity.OptionType == ProjectOptionTypes.Developer || entity.OptionType == ProjectOptionTypes.Compiler ||
            request.OptionType == ProjectOptionTypes.Developer || request.OptionType == ProjectOptionTypes.Compiler)
        {
            throw new RequestValidationException("开发人和编译人选项由人员管理统一维护，请在人员管理中调整操作人员信息。");
        }
        if (entity.RowVersion != request.RowVersion)
        {
            throw new ConflictException("该选项已被其他用户修改，请刷新后重试。");
        }

        var value = NormalizeValue(request.OptionValue);
        var staffGroup = await NormalizeStaffGroupAsync(
            request.OptionType, request.StaffGroup, cancellationToken);
        var personnelGroup = await NormalizePersonnelGroupAsync(
            request.OptionType, staffGroup, request.PersonnelGroup, cancellationToken);
        var programGroup = await NormalizeProgramGroupAsync(
            request.OptionType, request.ProgramGroup, cancellationToken);
        await EnsureUniqueAsync(request.OptionType, value, staffGroup, personnelGroup, id, cancellationToken);
        var oldValue = entity.OptionValue;
        var oldType = entity.OptionType;
        var oldStaffGroup = entity.StaffGroup;
        var oldPersonnelGroup = entity.PersonnelGroup;
        entity.OptionType = request.OptionType;
        entity.OptionValue = value;
        entity.StaffGroup = staffGroup;
        entity.PersonnelGroup = personnelGroup;
        entity.ProgramGroup = programGroup;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedBy = CurrentUsername();
        entity.UpdatedAt = DateTime.Now;
        entity.RowVersion++;
        if (oldType == ProjectOptionTypes.StaffGroup &&
            !string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
        {
            var children = await db.ProjectOptions
                .Where(x => (x.OptionType == ProjectOptionTypes.Developer ||
                             x.OptionType == ProjectOptionTypes.Compiler ||
                             x.OptionType == ProjectOptionTypes.ProgramGroup ||
                             x.OptionType == ProjectOptionTypes.ProgramName) &&
                            x.StaffGroup == oldValue)
                .ToListAsync(cancellationToken);
            foreach (var child in children)
            {
                child.StaffGroup = value;
                child.UpdatedBy = CurrentUsername();
                child.UpdatedAt = DateTime.Now;
                child.RowVersion++;
            }
        }
        if (oldType == ProjectOptionTypes.PersonnelGroup &&
            !string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
        {
            var children = await db.ProjectOptions
                .Where(x => (x.OptionType == ProjectOptionTypes.ProgramGroup ||
                             x.OptionType == ProjectOptionTypes.ProgramName) &&
                            x.PersonnelGroup == oldValue)
                .ToListAsync(cancellationToken);
            foreach (var child in children)
            {
                child.PersonnelGroup = value;
                child.UpdatedBy = CurrentUsername();
                child.UpdatedAt = DateTime.Now;
                child.RowVersion++;
            }
            var users = await db.Users
                .Where(x => x.RoleCode == "USER" && x.PersonnelGroup == oldValue)
                .ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                user.PersonnelGroup = value;
                user.UpdatedAt = DateTime.Now;
            }
        }
        if (oldType == ProjectOptionTypes.ProgramGroup)
        {
            // 1) 专案名称本身 OptionValue 变更 → 同步 PROGRAM_NAME.ProgramGroup
            if (!string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
            {
                var children = await db.ProjectOptions
                    .Where(x => x.OptionType == ProjectOptionTypes.ProgramName &&
                                x.ProgramGroup == oldValue)
                    .ToListAsync(cancellationToken);
                foreach (var child in children)
                {
                    child.ProgramGroup = value;
                    child.UpdatedBy = CurrentUsername();
                    child.UpdatedAt = DateTime.Now;
                    child.RowVersion++;
                }
            }
            // 2) 专案所属项目组变更 → 同步 PROGRAM_NAME.StaffGroup
            if (!string.Equals(oldStaffGroup, staffGroup, StringComparison.OrdinalIgnoreCase))
            {
                var children = await db.ProjectOptions
                    .Where(x => x.OptionType == ProjectOptionTypes.ProgramName &&
                                x.ProgramGroup == value)
                    .ToListAsync(cancellationToken);
                foreach (var child in children)
                {
                    child.StaffGroup = staffGroup;
                    child.UpdatedBy = CurrentUsername();
                    child.UpdatedAt = DateTime.Now;
                    child.RowVersion++;
                }
            }
            // 3) 专案所属人员组变更 → 同步 PROGRAM_NAME.PersonnelGroup
            if (!string.Equals(oldPersonnelGroup, personnelGroup, StringComparison.OrdinalIgnoreCase))
            {
                var children = await db.ProjectOptions
                    .Where(x => x.OptionType == ProjectOptionTypes.ProgramName &&
                                x.ProgramGroup == value)
                    .ToListAsync(cancellationToken);
                foreach (var child in children)
                {
                    child.PersonnelGroup = personnelGroup;
                    child.UpdatedBy = CurrentUsername();
                    child.UpdatedAt = DateTime.Now;
                    child.RowVersion++;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<ProjectOptionDto>.Ok(ToDto(entity), "项目可选项更新成功"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string id,
        [FromQuery] long rowVersion,
        CancellationToken cancellationToken)
    {
        var entities = await db.ProjectOptions.Where(x => x.Id == id).ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目可选项不存在。");
        if (!CanManageOptionType(entity.OptionType))
        {
            return Forbid();
        }
        if (entity.OptionType == ProjectOptionTypes.Developer || entity.OptionType == ProjectOptionTypes.Compiler)
        {
            throw new RequestValidationException("开发人和编译人选项由人员管理统一维护，如需删除请直接在人员管理中移除操作人员。");
        }
        if (entity.RowVersion != rowVersion)
        {
            throw new ConflictException("该选项已被其他用户修改，请刷新后重试。");
        }

        if (entity.OptionType == ProjectOptionTypes.StaffGroup &&
            (await db.Users.AnyAsync(
                x => x.RoleCode == "USER" && x.StaffGroup == entity.OptionValue,
                cancellationToken) ||
             await db.ProjectOptions.AnyAsync(
                x => (x.OptionType == ProjectOptionTypes.ProgramGroup ||
                      x.OptionType == ProjectOptionTypes.ProgramName) &&
                     x.StaffGroup == entity.OptionValue,
                cancellationToken)))
        {
            throw new ConflictException("该项目组下仍有关联的操作人员、专案或程序名，请先调整后再删除项目组。");
        }

        if (entity.OptionType == ProjectOptionTypes.PersonnelGroup &&
            (await db.Users.AnyAsync(
                x => x.RoleCode == "USER" && x.PersonnelGroup == entity.OptionValue,
                cancellationToken) ||
             await db.ProjectOptions.AnyAsync(
                x => x.OptionType == ProjectOptionTypes.ProgramGroup &&
                     x.PersonnelGroup == entity.OptionValue,
                cancellationToken)))
        {
            throw new ConflictException("该人员组下仍有关联的操作人员或专案，请先调整后再删除人员组。");
        }

        if (entity.OptionType == ProjectOptionTypes.ProgramGroup &&
            await db.ProjectOptions.AnyAsync(
                x => x.OptionType == ProjectOptionTypes.ProgramName &&
                     x.ProgramGroup == entity.OptionValue,
                cancellationToken))
        {
            throw new ConflictException("该专案下仍有关联程序名，请先调整程序名归属后再删除专案。");
        }

        db.ProjectOptions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "项目可选项删除成功"));
    }

    private async Task EnsureUniqueAsync(
        string optionType,
        string optionValue,
        string? staffGroup,
        string? personnelGroup,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        // PROGRAM_GROUP 和 PROGRAM_NAME 按"项目组+人员组+名称"维度查重
        // 不同人员组下允许相同专案名称或程序名
        var scopedTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            ProjectOptionTypes.ProgramGroup,
            ProjectOptionTypes.ProgramName
        };

        if (!scopedTypes.Contains(optionType))
        {
            // 非范围类型：保持原逻辑，同一分类下不允许重名
            var candidates = await db.ProjectOptions.AsNoTracking()
                .Where(x => x.OptionType == optionType && (excludedId == null || x.Id != excludedId))
                .Select(x => x.OptionValue)
                .ToListAsync(cancellationToken);
            if (candidates.Any(x => string.Equals(x, optionValue, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException("同一分类中已存在相同的选项内容。");
            }
            return;
        }

        // PROGRAM_GROUP / PROGRAM_NAME：在"同一项目组+同一人员组"范围内查重
        var sg = staffGroup ?? string.Empty;
        var pg = personnelGroup ?? string.Empty;

        var scopeCandidates = await db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == optionType && (excludedId == null || x.Id != excludedId))
            .Select(x => new { x.OptionValue, x.StaffGroup, x.PersonnelGroup })
            .ToListAsync(cancellationToken);

        var conflict = scopeCandidates.Any(x =>
            string.Equals(x.OptionValue, optionValue, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.StaffGroup ?? string.Empty, sg, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.PersonnelGroup ?? string.Empty, pg, StringComparison.OrdinalIgnoreCase));

        if (conflict)
        {
            throw new ConflictException("同一项目组和人员组下已存在相同的选项内容。");
        }
    }

    private bool CanAccessProjectSettings() =>
        HasPermission(SystemPermissions.OptionManage) ||
        HasPermission(SystemPermissions.ProgramRelationManage);

    private bool CanManageOptionType(string optionType) =>
        HasPermission(SystemPermissions.OptionManage) ||
        (HasPermission(SystemPermissions.ProgramRelationManage) &&
         optionType is ProjectOptionTypes.ProgramGroup or ProjectOptionTypes.ProgramName);

    private bool HasPermission(string permission) =>
        User.IsInRole("ADMIN") || User.HasClaim(SystemPermissions.ClaimType, permission);

    private async Task<string?> NormalizeProgramGroupAsync(
        string optionType,
        string? programGroup,
        CancellationToken cancellationToken)
    {
        if (optionType != ProjectOptionTypes.ProgramName)
        {
            if (!string.IsNullOrWhiteSpace(programGroup))
            {
                throw new RequestValidationException("只有程序名选项可以设置所属专案。");
            }
            return null;
        }

        var normalized = NormalizeValue(programGroup ?? string.Empty);
        var groups = await db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        if (!groups.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new RequestValidationException("程序名所属专案不存在，请先在项目设置中维护专案。");
        }
        return normalized;
    }

    private async Task<string?> NormalizeStaffGroupAsync(
        string optionType,
        string? staffGroup,
        CancellationToken cancellationToken)
    {
        if (optionType != ProjectOptionTypes.Developer &&
            optionType != ProjectOptionTypes.Compiler &&
            optionType != ProjectOptionTypes.PersonnelGroup &&
            optionType != ProjectOptionTypes.ProgramGroup &&
            optionType != ProjectOptionTypes.ProgramName)
        {
            if (!string.IsNullOrWhiteSpace(staffGroup))
            {
                throw new RequestValidationException("只有开发人、编译人、人员组、专案和程序名选项可以设置所属项目组。");
            }
            return null;
        }

        if (string.IsNullOrWhiteSpace(staffGroup))
        {
            if (optionType == ProjectOptionTypes.ProgramName)
            {
                return null;
            }
            throw new RequestValidationException("所属项目组不能为空，请先在项目设置中维护项目组。");
        }

        var normalized = staffGroup.Trim();
        var groups = await db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.StaffGroup)
            .Select(x => x.OptionValue)
            .ToListAsync(cancellationToken);
        if (!groups.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new RequestValidationException("所属项目组不存在，请先在项目设置中维护项目组。");
        }
        return normalized;
    }

    private async Task<string?> NormalizePersonnelGroupAsync(
        string optionType,
        string? staffGroup,
        string? personnelGroup,
        CancellationToken cancellationToken)
    {
        if (optionType != ProjectOptionTypes.ProgramGroup && optionType != ProjectOptionTypes.ProgramName)
        {
            if (!string.IsNullOrWhiteSpace(personnelGroup))
            {
                throw new RequestValidationException("只有专案名称和程序名选项可以设置所属人员组。");
            }
            return null;
        }

        if (string.IsNullOrWhiteSpace(personnelGroup))
        {
            // 专案名称必须指定所属人员组
            if (optionType == ProjectOptionTypes.ProgramGroup)
            {
                throw new RequestValidationException("所属人员组不能为空，请先在项目设置中维护人员组。");
            }
            return null; // 程序名的人员组是可选的
        }

        var normalized = personnelGroup.Trim();
        var groups = await db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup)
            .Select(x => new { x.OptionValue, x.StaffGroup })
            .ToListAsync(cancellationToken);
        var match = groups.FirstOrDefault(x =>
            string.Equals(x.OptionValue, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new RequestValidationException("所属人员组不存在，请先在项目设置中维护人员组。");
        }
        // 如果有 StaffGroup，验证人员组属于该项目组
        if (!string.IsNullOrWhiteSpace(staffGroup) &&
            !string.Equals(match.StaffGroup, staffGroup, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("所选人员组不属于当前项目组。");
        }
        return normalized;
    }

    private static string NormalizeImportedProgramGroup(
        string? programGroup,
        IReadOnlySet<string> availableGroups)
    {
        var normalized = NormalizeValue(programGroup ?? string.Empty);
        if (!availableGroups.Contains(normalized))
        {
            throw new RequestValidationException(
                $"程序名“所属专案”不存在：{normalized}，请先导入或维护专案。");
        }
        return normalized;
    }

    private static string? NormalizeImportedNonProgramGroup(string? programGroup)
    {
        if (!string.IsNullOrWhiteSpace(programGroup))
        {
            throw new RequestValidationException("只有程序名选项可以填写所属专案。");
        }
        return null;
    }

    private static string NormalizeImportedStaffGroup(
        string? staffGroup,
        IReadOnlySet<string> availableGroups)
    {
        var normalized = NormalizeValue(staffGroup ?? string.Empty);
        if (!availableGroups.Contains(normalized))
        {
            throw new RequestValidationException(
                $"所属项目组不存在：{normalized}，请先导入或维护项目组。");
        }
        return normalized;
    }

    private static string? NormalizeImportedNonStaffGroup(string? staffGroup)
    {
        if (!string.IsNullOrWhiteSpace(staffGroup))
        {
            throw new RequestValidationException("只有开发人、编译人、人员组、专案和程序名选项可以填写所属项目组。");
        }
        return null;
    }

    private static string? NormalizeImportedPersonnelGroup(
        string? personnelGroup,
        string? staffGroup,
        IReadOnlyList<(string OptionValue, string? StaffGroup)> availableGroups)
    {
        if (string.IsNullOrWhiteSpace(personnelGroup))
        {
            return null; // 人员组是可选的
        }
        var normalized = personnelGroup.Trim();
        var match = availableGroups.FirstOrDefault(x =>
            string.Equals(x.OptionValue, normalized, StringComparison.OrdinalIgnoreCase));
        if (match.OptionValue is null)
        {
            throw new RequestValidationException(
                $"所属人员组不存在：{normalized}，请先导入或维护人员组。");
        }
        if (!string.IsNullOrWhiteSpace(staffGroup) &&
            !string.Equals(match.StaffGroup, staffGroup, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"所选人员组“{normalized}”不属于当前项目组。");
        }
        return normalized;
    }

    private static string? NormalizeImportedNonPersonnelGroup(string? personnelGroup)
    {
        if (!string.IsNullOrWhiteSpace(personnelGroup))
        {
            throw new RequestValidationException("只有专案名称和程序名选项可以填写所属人员组。");
        }
        return null;
    }

    private static string NormalizeValue(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new RequestValidationException("选项内容不能为空。");
        }
        return normalized;
    }

    private static string ImportKey(string optionType, string optionValue) =>
        $"{optionType}\u001f{optionValue.Trim()}";

    private string CurrentUsername() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    private static ProjectOptionDto ToDto(ProjectOption entity) =>
        new(
            entity.Id,
            entity.OptionType,
            entity.OptionValue,
            entity.StaffGroup,
            entity.PersonnelGroup,
            entity.ProgramGroup,
            entity.SortOrder,
            entity.UpdatedBy,
            entity.UpdatedAt,
            entity.RowVersion);
}
