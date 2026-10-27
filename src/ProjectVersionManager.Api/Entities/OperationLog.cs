namespace ProjectVersionManager.Api.Entities;

public sealed class OperationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BusinessId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public DateTime OperationTime { get; set; }
    public string? ProgramGroup { get; set; }
    public string? ProgramName { get; set; }
    public string? ProgramVersion { get; set; }
    public string? OperationDetail { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
