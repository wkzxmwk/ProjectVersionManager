using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Controllers;

[ApiController]
[Authorize(Policy = SystemPermissions.LogView)]
[Route("operation-logs")]
public sealed class OperationLogsController(PvmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OperationLogDto>>>> GetList(
        [FromQuery] OperationLogQuery query,
        CancellationToken cancellationToken)
    {
        var logs = db.OperationLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.OperationType))
        {
            logs = logs.Where(x => x.OperationType == query.OperationType);
        }
        if (query.StartTime.HasValue)
        {
            logs = logs.Where(x => x.OperationTime >= query.StartTime.Value);
        }
        if (query.EndTime.HasValue)
        {
            logs = logs.Where(x => x.OperationTime <= query.EndTime.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            logs = logs.Where(x =>
                x.Operator.Contains(keyword) ||
                x.BusinessId.Contains(keyword));
        }

        var total = await logs.CountAsync(cancellationToken);
        var allItems = await logs
            .OrderByDescending(log => log.OperationTime)
            .Select(log => new OperationLogDto(
                log.Id,
                log.BusinessId,
                log.OperationType,
                log.Operator,
                log.OperationTime,
                log.ProgramName,
                log.ProgramVersion,
                log.BeforeData,
                log.AfterData,
                log.IpAddress,
                log.UserAgent))
            .ToListAsync(cancellationToken);
        var items = allItems
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return Ok(ApiResponse<PagedResult<OperationLogDto>>.Ok(
            new PagedResult<OperationLogDto>(items, query.Page, query.PageSize, total)));
    }
}
