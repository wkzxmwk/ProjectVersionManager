namespace ProjectVersionManager.Api.Entities;

public sealed class ProjectVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime ProjectDate { get; set; }
    public string ProjectType { get; set; } = string.Empty;
    public string Developer { get; set; } = string.Empty;
    public string ProgramGroup { get; set; } = string.Empty;
    public string? PersonnelGroup { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string ProgramVersionNumber { get; set; } = string.Empty;
    public string? Compiler { get; set; }
    public DateTime? CompileTime { get; set; }
    public string? CompiledProgramVersion { get; set; }
    public string? VersionDescription { get; set; }
    public DateTime? ReleaseTime { get; set; }
    public string? BranchName { get; set; }
    public string? Remark1 { get; set; }
    public string? Remark2 { get; set; }
    public string Status { get; set; } = "PENDING";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public long RowVersion { get; set; } = 1;
}
