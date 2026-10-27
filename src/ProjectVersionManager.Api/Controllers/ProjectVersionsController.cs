using System.Security.Claims;
using System.Text.Json;
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
[Route("project-versions")]
public sealed class ProjectVersionsController(
    PvmDbContext db,
    ProjectOptionCatalog optionCatalog) : ControllerBase
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private const long MaximumImportFileSize = 5 * 1024 * 1024;
    private const long MaximumImportRequestSize = MaximumImportFileSize + 64 * 1024;
    private const string ConcurrentDuplicateVersionMessage =
        "检测到其他人员刚刚提交了相同专案、程序名和程序版本，请刷新后修改程序版本号。";

    [HttpGet]
    [Authorize(Policy = SystemPermissions.VersionView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectVersionDto>>>> GetList(
        [FromQuery] ProjectVersionQuery query,
        CancellationToken cancellationToken)
    {
        var source = query.DeletedOnly
            ? db.ProjectVersions.IgnoreQueryFilters().AsNoTracking().Where(x => x.IsDeleted)
            : db.ProjectVersions.AsNoTracking();
        source = await ApplyStaffGroupFilterAsync(source, cancellationToken);
        var filtered = ApplyFilter(source, query);
        var total = await filtered.CountAsync(cancellationToken);
        // Materialize before paging to retain the existing server-side filter and
        // paging behavior across database providers.
        var filteredEntities = await filtered
            .OrderByDescending(x => x.ProjectDate)
            .ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        var entities = filteredEntities
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        var items = entities.Select(x => x.ToDto()).ToList();

        return Ok(ApiResponse<PagedResult<ProjectVersionDto>>.Ok(
            new PagedResult<ProjectVersionDto>(items, query.Page, query.PageSize, total)));
    }

    [HttpGet("status-counts")]
    [Authorize(Policy = SystemPermissions.VersionView)]
    public async Task<ActionResult<ApiResponse<ProjectVersionStatusCountsDto>>> GetStatusCounts(
        CancellationToken cancellationToken)
    {
        var source = db.ProjectVersions.AsNoTracking();
        // 状态统计按登录用户范围过滤：
        // - 管理员：统计全部
        // - 非管理员且人员组与项目组同名（或未分配人员组）：统计整个项目组
        // - 非管理员且人员组与项目组不同名：仅统计当前登录用户作为开发人的记录
        if (!IsAdmin())
        {
            var sg = CurrentStaffGroup();
            if (string.IsNullOrEmpty(sg))
            {
                return Ok(ApiResponse<ProjectVersionStatusCountsDto>.Ok(
                    new ProjectVersionStatusCountsDto(0, 0, 0, 0, 0, 0, 0)));
            }

            if (IsPersonnelGroupUnrestricted())
            {
                source = await ApplyStaffGroupFilterAsync(source, cancellationToken);
            }
            else
            {
                var displayName = CurrentDisplayName();
                if (string.IsNullOrEmpty(displayName))
                {
                    return Ok(ApiResponse<ProjectVersionStatusCountsDto>.Ok(
                        new ProjectVersionStatusCountsDto(0, 0, 0, 0, 0, 0, 0)));
                }
                source = source.Where(x => x.Developer == displayName);
            }
        }

        var groupedCounts = await source
            .GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int CountStatus(string status) => groupedCounts
            .Where(item => string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Count);
        var pending = CountStatus("PENDING");
        var waitCompile = CountStatus("WAIT_COMPILE");
        var compiled = CountStatus("COMPILED");
        var preReleased = CountStatus("PRE_RELEASED");
        var released = CountStatus("RELEASED");
        var commented = CountStatus("COMMENTED");

        return Ok(ApiResponse<ProjectVersionStatusCountsDto>.Ok(
            new ProjectVersionStatusCountsDto(
                pending + waitCompile + compiled + preReleased + commented,
                pending,
                waitCompile,
                compiled,
                preReleased,
                released,
                commented)));
    }

    [HttpGet("import-template")]
    [Authorize(Policy = SystemPermissions.VersionView)]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken cancellationToken)
    {
        var options = await db.ProjectOptions.AsNoTracking()
            .OrderBy(x => x.OptionType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.OptionValue)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        for (var index = 0; index < ProjectVersionImportParser.Headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = ProjectVersionImportParser.Headers[index];
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Font.FontColor = XLColor.White;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        sheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 1, ProjectVersionImportParser.Headers.Length).SetAutoFilter();
        sheet.Column(1).Width = 14;
        sheet.Columns(2, 7).Width = 18;
        sheet.Column(8).Width = 25;
        sheet.Columns(9, 11).Width = 18;
        sheet.Columns(12, 15).Width = 25;
        sheet.Column(16).Width = 15;
        sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns(10, 10).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        sheet.Columns(12, 12).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";

        var guide = workbook.Worksheets.Add("填写说明");
        guide.Cell("A1").Value = "项目版本批量导入说明";
        guide.Cell("A1").Style.Font.Bold = true;
        guide.Cell("A1").Style.Font.FontSize = 16;
        var instructions = new[]
        {
            "1. 请勿修改“项目版本导入”工作表名称及第一行标题。",
            "2. 必填列：项目时间、项目类型、开发人、专案名称、程序名、程序版本、状态。",
            "3. 日期建议使用 yyyy-MM-dd；日期时间建议使用 yyyy-MM-dd HH:mm:ss。",
            "4. 状态可填写：待释放、待编译、已编译、提前释放、已释放、已取消、已注释，或 PENDING、WAIT_COMPILE、COMPILED、PRE_RELEASED、RELEASED、CANCELED、COMMENTED。",
            "5. 项目类型、专案名称、程序名必须来自“项目设置”；开发人、编译人必须来自“人员管理”清单。",
            "6. 专案名称已绑定人员组时，需填写对应人员组；若人员组名称与项目组相同则不受人员组限制。",
            "7. 程序名必须属于所填专案；若所填专案已绑定人员组，还须校验程序名归属的人员组是否匹配。",
            "8. 人员组为可选项，若填写需在“项目设置”中已存在且属于当前项目组；留空表示不归属任何人员组。",
            "9. 相同专案名称 + 程序名 + 程序版本视为重复；重复记录会被跳过。",
            $"10. 仅支持 .xlsx，文件不超过 5 MB，单次最多 {ProjectVersionImportParser.MaximumRows} 行。",
            "11. 任意一行格式错误都会阻止整批导入，请根据页面错误提示修正后重试。"
        };
        for (var index = 0; index < instructions.Length; index++)
        {
            guide.Cell(index + 3, 1).Value = instructions[index];
        }
        guide.Column(1).Width = 110;

        var personnelNames = await db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER" && x.IsEnabled)
            .OrderBy(x => x.DisplayName ?? x.Username)
            .Select(x => x.DisplayName ?? x.Username)
            .ToListAsync(cancellationToken);

        var optionSheet = workbook.Worksheets.Add("可选项参考");
        optionSheet.Cell(1, 1).Value = "项目类型";
        {
            var values = options.Where(x => x.OptionType == ProjectOptionTypes.ProjectType).Select(x => x.OptionValue).ToList();
            for (var row = 0; row < values.Count; row++) optionSheet.Cell(row + 2, 1).Value = values[row];
        }
        optionSheet.Cell(1, 2).Value = "开发人（人员管理清单）";
        for (var row = 0; row < personnelNames.Count; row++) optionSheet.Cell(row + 2, 2).Value = personnelNames[row];
        optionSheet.Cell(1, 3).Value = "专案名称";
        optionSheet.Cell(1, 4).Value = "专案所属人员组";
        {
            var programGroupOptions = options
                .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.OptionValue)
                .ToList();
            for (var row = 0; row < programGroupOptions.Count; row++)
            {
                optionSheet.Cell(row + 2, 3).Value = programGroupOptions[row].OptionValue;
                optionSheet.Cell(row + 2, 4).Value = programGroupOptions[row].PersonnelGroup ?? "未绑定";
            }
        }
        optionSheet.Cell(1, 5).Value = "程序名";
        optionSheet.Cell(1, 6).Value = "程序名所属专案";
        {
            var programNameOptions = options
                .Where(x => x.OptionType == ProjectOptionTypes.ProgramName)
                .OrderBy(x => x.ProgramGroup)
                .ThenBy(x => x.OptionValue)
                .ToList();
            for (var row = 0; row < programNameOptions.Count; row++)
            {
                optionSheet.Cell(row + 2, 5).Value = programNameOptions[row].OptionValue;
                optionSheet.Cell(row + 2, 6).Value = programNameOptions[row].ProgramGroup ?? "未关联";
            }
        }
        optionSheet.Cell(1, 7).Value = "编译人（人员管理清单）";
        for (var row = 0; row < personnelNames.Count; row++) optionSheet.Cell(row + 2, 7).Value = personnelNames[row];
        optionSheet.Cell(1, 8).Value = "人员组";
        optionSheet.Cell(1, 9).Value = "人员组所属项目组";
        {
            var personnelGroupOptions = options
                .Where(x => x.OptionType == ProjectOptionTypes.PersonnelGroup)
                .OrderBy(x => x.StaffGroup)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.OptionValue)
                .ToList();
            for (var row = 0; row < personnelGroupOptions.Count; row++)
            {
                optionSheet.Cell(row + 2, 8).Value = personnelGroupOptions[row].OptionValue;
                optionSheet.Cell(row + 2, 9).Value = personnelGroupOptions[row].StaffGroup ?? "未绑定";
            }
        }
        optionSheet.Row(1).Style.Font.Bold = true;
        optionSheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF0FF");
        optionSheet.Columns().Width = 24;
        optionSheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "项目版本批量导入模板.xlsx");
    }

    [Authorize(Policy = SystemPermissions.VersionImport)]
    [HttpPost("import")]
    [RequestSizeLimit(MaximumImportRequestSize)]
    public async Task<ActionResult<ApiResponse<ProjectVersionImportResultDto>>> Import(
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

        IReadOnlyList<SaveProjectVersionRequest> importedRows;
        try
        {
            await using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "项目版本导入")
                ?? workbook.Worksheets.FirstOrDefault()
                ?? throw new RequestValidationException("Excel 文件中没有可读取的工作表。");
            importedRows = ProjectVersionImportParser.Parse(sheet);
        }
        catch (RequestValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RequestValidationException($"无法读取 Excel 文件：{exception.Message}");
        }

        await optionCatalog.EnsureValidAsync(importedRows, cancellationToken);

        if (!IsAdmin())
        {
            foreach (var row in importedRows)
            {
                await ValidateStaffGroupAccessAsync(row, cancellationToken);
            }
        }

        var existingVersions = await db.ProjectVersions.AsNoTracking()
            .Select(x => new { x.ProgramGroup, x.ProgramName, x.ProgramVersionNumber })
            .ToListAsync(cancellationToken);
        var uniqueKeys = existingVersions
            .Select(x => ImportKey(x.ProgramGroup, x.ProgramName, x.ProgramVersionNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.Now;
        var username = CurrentUsername();
        var created = new List<ProjectVersion>();
        var skippedDuplicates = 0;
        foreach (var request in importedRows)
        {
            if (!uniqueKeys.Add(ImportKey(
                    request.ProgramGroup,
                    request.ProgramName,
                    request.ProgramVersion)))
            {
                skippedDuplicates++;
                continue;
            }

            var entity = new ProjectVersion
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = now,
                CreatedBy = username,
                UpdatedAt = now,
                UpdatedBy = username,
                RowVersion = 1
            };
            request.Apply(entity);
            created.Add(entity);
        }

        if (created.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.ProjectVersions.AddRange(created);
            foreach (var entity in created)
            {
                db.OperationLogs.Add(BuildLog("CREATE", entity, null, entity));
            }
            await SaveChangesWithDuplicateHandlingAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var result = new ProjectVersionImportResultDto(
            importedRows.Count,
            created.Count,
            skippedDuplicates);
        return Ok(ApiResponse<ProjectVersionImportResultDto>.Ok(
            result,
            $"成功导入 {created.Count} 条版本记录，跳过 {skippedDuplicates} 条重复记录。"));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = SystemPermissions.VersionView)]
    public async Task<ActionResult<ApiResponse<ProjectVersionDto>>> Get(
        string id,
        CancellationToken cancellationToken)
    {
        var source = db.ProjectVersions.AsNoTracking().Where(x => x.Id == id);
        source = await ApplyStaffGroupFilterAsync(source, cancellationToken);
        var entities = await source.ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目版本记录不存在或已删除。");
        return Ok(ApiResponse<ProjectVersionDto>.Ok(entity.ToDto()));
    }

    [Authorize(Policy = SystemPermissions.VersionCreate)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProjectVersionDto>>> Create(
        [FromBody] SaveProjectVersionRequest request,
        CancellationToken cancellationToken)
    {
        await optionCatalog.EnsureValidAsync(request, null, cancellationToken);
        await ValidateStaffGroupAccessAsync(request, cancellationToken);
        await EnsureNoDuplicateAsync(request, null, cancellationToken);
        var now = DateTime.Now;
        var entity = new ProjectVersion
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = now,
            CreatedBy = CurrentUsername(),
            UpdatedAt = now,
            UpdatedBy = CurrentUsername(),
            RowVersion = 1
        };
        request.Apply(entity);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProjectVersions.Add(entity);
        db.OperationLogs.Add(BuildLog("CREATE", entity, null, entity));
        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = entity.Id },
            ApiResponse<ProjectVersionDto>.Ok(entity.ToDto(), "项目版本创建成功"));
    }

    [Authorize(Policy = SystemPermissions.VersionUpdate)]
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProjectVersionDto>>> Update(
        string id,
        [FromBody] SaveProjectVersionRequest request,
        CancellationToken cancellationToken)
    {
        var entities = await db.ProjectVersions.Where(x => x.Id == id).ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目版本记录不存在或已删除。");
        if (entity.RowVersion != request.RowVersion)
        {
            throw new ConflictException("该记录已被其他用户修改，请刷新后重试。");
        }

        if (!IsAdmin())
        {
            var scopedSource = db.ProjectVersions.AsNoTracking().Where(x => x.Id == id);
            var scopedCheck = await ApplyStaffGroupFilterAsync(scopedSource, cancellationToken);
            if (!await scopedCheck.AnyAsync(cancellationToken))
            {
                throw new ForbiddenException("该记录不属于您所在的项目组，无法修改。");
            }
        }

        await optionCatalog.EnsureValidAsync(request, entity, cancellationToken);
        await ValidateStaffGroupAccessAsync(request, cancellationToken);
        await EnsureNoDuplicateAsync(request, id, cancellationToken);
        var before = entity.ToDto();
        request.Apply(entity);
        entity.UpdatedAt = DateTime.Now;
        entity.UpdatedBy = CurrentUsername();
        entity.RowVersion++;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.OperationLogs.Add(BuildLog("UPDATE", entity, before, entity));
        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<ProjectVersionDto>.Ok(entity.ToDto(), "项目版本更新成功"));
    }

    [Authorize(Policy = SystemPermissions.VersionDelete)]
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string id,
        [FromQuery] long rowVersion,
        CancellationToken cancellationToken)
    {
        var entities = await db.ProjectVersions.Where(x => x.Id == id).ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目版本记录不存在或已删除。");
        if (entity.RowVersion != rowVersion)
        {
            throw new ConflictException("该记录已被其他用户修改，请刷新后重试。");
        }

        if (!IsAdmin())
        {
            var scopedSource = db.ProjectVersions.AsNoTracking().Where(x => x.Id == id);
            var scopedCheck = await ApplyStaffGroupFilterAsync(scopedSource, cancellationToken);
            if (!await scopedCheck.AnyAsync(cancellationToken))
            {
                throw new ForbiddenException("该记录不属于您所在的项目组，无法删除。");
            }
        }

        var before = entity.ToDto();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        entity.UpdatedBy = CurrentUsername();
        entity.RowVersion++;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.OperationLogs.Add(BuildLog("DELETE", entity, before, entity));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "项目版本已删除"));
    }

    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{id}/permanent")]
    public async Task<ActionResult<ApiResponse<object>>> PermanentDelete(
        string id,
        [FromQuery] long rowVersion,
        CancellationToken cancellationToken)
    {
        var entities = await db.ProjectVersions
            .IgnoreQueryFilters()
            .Where(x => x.Id == id)
            .ToListAsync(cancellationToken);
        var entity = entities.SingleOrDefault()
            ?? throw new NotFoundException("项目版本记录不存在。");
        if (entity.RowVersion != rowVersion)
        {
            throw new ConflictException("该记录已被其他用户修改，请刷新后重试。");
        }

        var before = entity.ToDto();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.OperationLogs.Add(BuildLog("DELETE", entity, before, null));
        db.ProjectVersions.Remove(entity);
        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "项目版本记录已物理删除"));
    }

    [Authorize(Policy = SystemPermissions.VersionDelete)]
    [HttpPost("batch-delete")]
    public async Task<ActionResult<ApiResponse<object>>> BatchDelete(
        [FromBody] BatchVersionMutationRequest request,
        CancellationToken cancellationToken)
    {
        var requested = request.Items
            .GroupBy(x => x.Id)
            .Select(group => group.First())
            .ToList();
        var ids = requested.Select(x => x.Id).ToList();
        var entities = await db.ProjectVersions
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        EnsureBatchMatches(requested, entities, "删除");

        if (!IsAdmin())
        {
            var scopedSource = db.ProjectVersions.AsNoTracking();
            var scopedCheck = await ApplyStaffGroupFilterAsync(scopedSource, cancellationToken);
            var allowedIds = await scopedCheck.Select(x => x.Id).ToListAsync(cancellationToken);
            var crossGroupIds = ids.Where(id => !allowedIds.Contains(id)).ToList();
            if (crossGroupIds.Count > 0)
            {
                throw new ForbiddenException("部分记录不属于您所在的项目组，无法删除。");
            }
        }

        var now = DateTime.Now;
        var username = CurrentUsername();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var entity in entities)
        {
            var before = entity.ToDto();
            entity.IsDeleted = true;
            entity.UpdatedAt = now;
            entity.UpdatedBy = username;
            entity.RowVersion++;
            db.OperationLogs.Add(BuildLog("DELETE", entity, before, entity));
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            new { count = entities.Count },
            $"已将 {entities.Count} 条记录移入回收站"));
    }

    [Authorize(Policy = SystemPermissions.VersionDelete)]
    [HttpPost("batch-restore")]
    public async Task<ActionResult<ApiResponse<object>>> BatchRestore(
        [FromBody] BatchVersionMutationRequest request,
        CancellationToken cancellationToken)
    {
        var requested = request.Items
            .GroupBy(x => x.Id)
            .Select(group => group.First())
            .ToList();
        var ids = requested.Select(x => x.Id).ToList();
        var entities = await db.ProjectVersions
            .IgnoreQueryFilters()
            .Where(x => x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        EnsureBatchMatches(requested, entities, "恢复");

        if (!IsAdmin())
        {
            var scopedSource = db.ProjectVersions.IgnoreQueryFilters().AsNoTracking().Where(x => x.IsDeleted);
            var scopedCheck = await ApplyStaffGroupFilterAsync(scopedSource, cancellationToken);
            var allowedIds = await scopedCheck.Select(x => x.Id).ToListAsync(cancellationToken);
            var crossGroupIds = ids.Where(id => !allowedIds.Contains(id)).ToList();
            if (crossGroupIds.Count > 0)
            {
                throw new ForbiddenException("部分记录不属于您所在的项目组，无法恢复。");
            }
        }

        var now = DateTime.Now;
        var username = CurrentUsername();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var entity in entities)
        {
            var before = entity.ToDto();
            entity.IsDeleted = false;
            entity.UpdatedAt = now;
            entity.UpdatedBy = username;
            entity.RowVersion++;
            db.OperationLogs.Add(BuildLog("RESTORE", entity, before, entity));
        }
        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            new { count = entities.Count },
            $"已恢复 {entities.Count} 条记录"));
    }

    [HttpGet("export")]
    [Authorize(Policy = SystemPermissions.VersionView)]
    public async Task<IActionResult> Export(
        [FromQuery] ProjectVersionQuery query,
        CancellationToken cancellationToken)
    {
        var source = query.DeletedOnly
            ? db.ProjectVersions.IgnoreQueryFilters().AsNoTracking().Where(x => x.IsDeleted)
            : db.ProjectVersions.AsNoTracking();
        source = await ApplyStaffGroupFilterAsync(source, cancellationToken);
        var items = await ApplyFilter(source, query)
            .OrderByDescending(x => x.ProjectDate)
            .ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本");
        var headers = new[]
        {
            "项目时间", "项目类型", "开发人", "专案名称", "人员组", "程序名", "程序版本", "程序版本描述", "编译人", "编译时间",
            "编译程序版本", "释放时间", "关联分支名", "备注1", "备注2", "状态",
            "创建人", "创建时间", "最后修改人", "最后修改时间", "RowVersion"
        };
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF0FF");

        for (var row = 0; row < items.Count; row++)
        {
            var item = items[row];
            var values = new object?[]
            {
                item.ProjectDate, item.ProjectType, item.Developer, item.ProgramGroup, item.PersonnelGroup, item.ProgramName,
                item.ProgramVersionNumber, item.VersionDescription, item.Compiler, item.CompileTime, item.CompiledProgramVersion,
                item.ReleaseTime, item.BranchName, item.Remark1, item.Remark2,
                item.Status, item.CreatedBy, item.CreatedAt, item.UpdatedBy, item.UpdatedAt, item.RowVersion
            };
            for (var column = 0; column < values.Length; column++)
            {
                sheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(values[column]);
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(8, 42);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"项目版本_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    private IQueryable<ProjectVersion> ApplyFilter(
        IQueryable<ProjectVersion> source,
        ProjectVersionQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            source = source.Where(x =>
                x.ProgramName.Contains(keyword) ||
                x.ProgramVersionNumber.Contains(keyword) ||
                (x.VersionDescription != null && x.VersionDescription.Contains(keyword)) ||
                (x.BranchName != null && x.BranchName.Contains(keyword)));
        }
        if (!string.IsNullOrWhiteSpace(query.ProjectType))
        {
            source = source.Where(x => x.ProjectType == query.ProjectType);
        }
        if (!string.IsNullOrWhiteSpace(query.Developer))
        {
            source = source.Where(x => x.Developer == query.Developer);
        }
        if (!string.IsNullOrWhiteSpace(query.Compiler))
        {
            var compiler = query.Compiler.Trim();
            source = source.Where(x => x.Compiler == compiler);
        }
        if (!string.IsNullOrWhiteSpace(query.ProgramGroup))
        {
            source = source.Where(x => x.ProgramGroup == query.ProgramGroup);
        }
        if (!string.IsNullOrWhiteSpace(query.ProgramName))
        {
            var programName = query.ProgramName.Trim();
            source = source.Where(x => x.ProgramName == programName);
        }
        if (!string.IsNullOrWhiteSpace(query.PersonnelGroup))
        {
            var pg = query.PersonnelGroup.Trim();
            source = source.Where(x => x.PersonnelGroup == pg);
        }
        if (!string.IsNullOrWhiteSpace(query.StaffGroup) && IsAdmin())
        {
            var sg = query.StaffGroup.Trim();
            source = source.Where(x =>
                db.Users.AsNoTracking().Any(u =>
                    u.RoleCode == "USER" &&
                    (u.DisplayName ?? u.Username) == x.Developer &&
                    u.StaffGroup == sg) ||
                (x.Compiler != null && db.Users.AsNoTracking().Any(u =>
                    u.RoleCode == "USER" &&
                    (u.DisplayName ?? u.Username) == x.Compiler &&
                    u.StaffGroup == sg)) ||
                db.ProjectOptions.AsNoTracking().Any(opt =>
                    opt.OptionType == ProjectOptionTypes.ProgramGroup &&
                    opt.OptionValue == x.ProgramGroup &&
                    opt.StaffGroup == sg));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            if (string.Equals(status, "UNRELEASED", StringComparison.Ordinal))
            {
                source = source.Where(x => x.Status != "RELEASED" && x.Status != "CANCELED");
            }
            else
            {
                source = source.Where(x => x.Status == status);
            }
        }
        else if (query.ExcludeReleased)
        {
            source = source.Where(x => x.Status != "RELEASED" && x.Status != "CANCELED");
        }
        if (query.StartDate.HasValue)
        {
            source = source.Where(x => x.ProjectDate >= query.StartDate.Value.Date);
        }
        if (query.EndDate.HasValue)
        {
            var exclusiveEnd = query.EndDate.Value.Date.AddDays(1);
            source = source.Where(x => x.ProjectDate < exclusiveEnd);
        }
        return source;
    }

    private async Task EnsureNoDuplicateAsync(
        SaveProjectVersionRequest request,
        string? excludedId,
        CancellationToken cancellationToken)
    {
        var programGroup = request.ProgramGroup.Trim().ToUpperInvariant();
        var programName = request.ProgramName.Trim().ToUpperInvariant();
        var programVersion = request.ProgramVersion.Trim().ToUpperInvariant();

        var duplicateDevelopers = await db.ProjectVersions.AsNoTracking()
            .Where(
                x => (excludedId == null || x.Id != excludedId) &&
                     x.ProgramGroup.ToUpper() == programGroup &&
                     x.ProgramName.ToUpper() == programName &&
                     x.ProgramVersionNumber.ToUpper() == programVersion)
            .Select(x => x.Developer)
            .ToListAsync(cancellationToken);
        if (duplicateDevelopers.Count > 0)
        {
            var developers = string.Join("、", duplicateDevelopers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(developers))
            {
                developers = "未知";
            }
            throw new ConflictException(
                $"项目组“{request.ProgramGroup.Trim()}”中的程序“{request.ProgramName.Trim()}”" +
                $"已存在版本“{request.ProgramVersion.Trim()}”（开发人：{developers}）。" +
                "请更改程序版本号后再保存。");
        }
    }

    private async Task SaveChangesWithDuplicateHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveVersionUniqueIndexViolation(exception))
        {
            throw new ConflictException(ConcurrentDuplicateVersionMessage);
        }
        catch (DbUpdateException exception) when (IsOperationLogTypeConstraintViolation(exception))
        {
            throw new RequestValidationException(
                "数据库操作日志类型约束 CK_PVM_LOG_OP_TYPE 与当前程序不一致，请先执行数据库修复脚本后再保存。");
        }
        catch (DbUpdateException exception) when (IsVersionDescriptionNullViolation(exception))
        {
            throw new RequestValidationException(
                "数据库将程序版本描述 VERSION_DESCRIPTION 设置成了必填列，请执行数据库修复脚本后再保存。");
        }
    }

    private static bool IsActiveVersionUniqueIndexViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("UX_PVM_PV_ACTIVE_UQ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsOperationLogTypeConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("ORA-02290", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("CK_PVM_LOG_OP_TYPE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsVersionDescriptionNullViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("ORA-01400", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("VERSION_DESCRIPTION", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string ImportKey(
        string programGroup,
        string programName,
        string programVersion) =>
        $"{programGroup.Trim()}\u001f{programName.Trim()}\u001f{programVersion.Trim()}";

    private OperationLog BuildLog(
        string operationType,
        ProjectVersion entity,
        object? before,
        object? after) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            BusinessId = entity.Id,
            OperationType = operationType,
            Operator = CurrentUsername(),
            OperationTime = DateTime.Now,
            ProgramGroup = entity.ProgramGroup,
            ProgramName = entity.ProgramName,
            ProgramVersion = entity.ProgramVersionNumber,
            OperationDetail = $"{operationType}: {entity.ProgramGroup}/{entity.ProgramName} {entity.ProgramVersionNumber}",
            BeforeData = before is null ? null : JsonSerializer.Serialize(before, AuditJsonOptions),
            AfterData = after is null ? null : JsonSerializer.Serialize(after, AuditJsonOptions),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()[..Math.Min(
                Request.Headers.UserAgent.ToString().Length, 1000)]
        };

    private string CurrentUsername() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    private string? CurrentStaffGroup() =>
        User.FindFirstValue("staff_group");

    private string? CurrentPersonnelGroup() =>
        User.FindFirstValue("personnel_group");

    private string? CurrentDisplayName() =>
        User.FindFirstValue("display_name");

    private bool IsAdmin() =>
        string.Equals(User.FindFirstValue(ClaimTypes.Role), "ADMIN", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断当前用户是否不受人员组限制：
    /// admin、未设置人员组、或人员组名称与项目组名称相同的情况
    /// </summary>
    private bool IsPersonnelGroupUnrestricted()
    {
        if (IsAdmin()) return true;
        var sg = CurrentStaffGroup();
        var pg = CurrentPersonnelGroup();
        if (string.IsNullOrEmpty(pg)) return true;
        return string.Equals(sg, pg, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetAllowedDeveloperValuesAsync(CancellationToken ct)
    {
        var sg = CurrentStaffGroup();
        if (string.IsNullOrEmpty(sg)) return [];
        var query = db.Users.AsNoTracking()
            .Where(x => x.RoleCode == "USER" && x.IsEnabled && x.StaffGroup == sg);
        if (!IsPersonnelGroupUnrestricted())
        {
            var pg = CurrentPersonnelGroup();
            query = query.Where(x => x.PersonnelGroup == pg);
        }
        return await query.Select(x => x.DisplayName ?? x.Username).ToListAsync(ct);
    }

    private async Task<List<string>> GetAllowedProgramGroupValuesAsync(CancellationToken ct)
    {
        var sg = CurrentStaffGroup();
        if (string.IsNullOrEmpty(sg)) return [];
        var query = db.ProjectOptions.AsNoTracking()
            .Where(x => x.OptionType == ProjectOptionTypes.ProgramGroup && x.StaffGroup == sg);
        if (!IsPersonnelGroupUnrestricted())
        {
            var pg = CurrentPersonnelGroup();
            query = query.Where(x => x.PersonnelGroup == pg);
        }
        return await query.Select(x => x.OptionValue).ToListAsync(ct);
    }

    private async Task<IQueryable<ProjectVersion>> ApplyStaffGroupFilterAsync(
        IQueryable<ProjectVersion> source,
        CancellationToken ct)
    {
        if (IsAdmin()) return source;
        var sg = CurrentStaffGroup();
        if (string.IsNullOrEmpty(sg)) return source.Where(_ => false);

        var developerValues = await GetAllowedDeveloperValuesAsync(ct);
        var programGroupValues = await GetAllowedProgramGroupValuesAsync(ct);

        // 人员组不受限：可以看到整个项目组的数据
        if (IsPersonnelGroupUnrestricted())
        {
            return source.Where(x =>
                (developerValues.Contains(x.Developer)) ||
                (programGroupValues.Contains(x.ProgramGroup)));
        }

        // 受人员组限制：只看到与当前人员组关联的记录
        var pg = CurrentPersonnelGroup();
        return source.Where(x =>
            x.PersonnelGroup == pg &&
            ((developerValues.Contains(x.Developer)) ||
             (programGroupValues.Contains(x.ProgramGroup))));
    }

    private async Task ValidateStaffGroupAccessAsync(
        SaveProjectVersionRequest request,
        CancellationToken ct)
    {
        if (IsAdmin()) return;
        var sg = CurrentStaffGroup();
        if (string.IsNullOrEmpty(sg))
        {
            throw new ForbiddenException("您未分配项目组，无法操作版本记录。请联系管理员设置您的项目组。");
        }

        var developerValues = await GetAllowedDeveloperValuesAsync(ct);
        if (!developerValues.Contains(request.Developer.Trim()))
        {
            throw new ForbiddenException($"开发人 \"{request.Developer}\" 不属于您所在的项目组，无法操作。");
        }

        var programGroupValues = await GetAllowedProgramGroupValuesAsync(ct);
        if (!programGroupValues.Contains(request.ProgramGroup.Trim()))
        {
            throw new ForbiddenException($"专案 \"{request.ProgramGroup}\" 不属于您所在的项目组，无法操作。");
        }

        // 人员组为可选维度，仅做存在性校验，不强制限制
        if (!string.IsNullOrWhiteSpace(request.PersonnelGroup))
        {
            var personnelGroupExists = await db.ProjectOptions.AsNoTracking()
                .AnyAsync(x => x.OptionType == ProjectOptionTypes.PersonnelGroup &&
                               x.OptionValue == request.PersonnelGroup.Trim(), ct);
            if (!personnelGroupExists)
            {
                throw new RequestValidationException($"人员组 \"{request.PersonnelGroup}\" 不存在，请刷新选项后重试。");
            }
        }
    }

    private static void EnsureBatchMatches(
        IReadOnlyCollection<VersionMutationItem> requested,
        IReadOnlyCollection<ProjectVersion> entities,
        string action)
    {
        if (requested.Count != entities.Count)
        {
            throw new ConflictException($"部分记录不存在或状态已变化，无法完成批量{action}，请刷新后重试。");
        }

        var versions = requested.ToDictionary(x => x.Id, x => x.RowVersion);
        if (entities.Any(entity => versions[entity.Id] != entity.RowVersion))
        {
            throw new ConflictException($"部分记录已被其他用户修改，无法完成批量{action}，请刷新后重试。");
        }
    }
}
