using System.ComponentModel.DataAnnotations;

namespace ProjectVersionManager.Api.Dtos;

public sealed class OperationLogQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 20;

    [StringLength(200)]
    public string? Keyword { get; set; }

    [StringLength(20)]
    public string? OperationType { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public sealed record OperationLogDto(
    string Id,
    string BusinessId,
    string OperationType,
    string Operator,
    DateTime OperationTime,
    string? ProgramName,
    string? ProgramVersion,
    string? BeforeData,
    string? AfterData,
    string? IpAddress,
    string? UserAgent);
