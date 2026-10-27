namespace ProjectVersionManager.Api.Entities;

public sealed class ProjectOption
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OptionType { get; set; } = string.Empty;
    public string OptionValue { get; set; } = string.Empty;
    public string? StaffGroup { get; set; }
    public string? PersonnelGroup { get; set; }
    public string? ProgramGroup { get; set; }
    public int SortOrder { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
