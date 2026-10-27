using System.ComponentModel.DataAnnotations;

namespace ProjectVersionManager.Api.Dtos;

public sealed class ProjectVersionQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 20;

    [StringLength(200)]
    public string? Keyword { get; set; }

    [StringLength(50)]
    public string? ProjectType { get; set; }

    [StringLength(100)]
    public string? Developer { get; set; }

    [StringLength(100)]
    public string? Compiler { get; set; }

    [StringLength(100)]
    public string? ProgramGroup { get; set; }

    [StringLength(200)]
    public string? ProgramName { get; set; }

    [StringLength(100)]
    public string? StaffGroup { get; set; }

    [StringLength(100)]
    public string? PersonnelGroup { get; set; }

    [StringLength(30), RegularExpression("UNRELEASED|PENDING|WAIT_COMPILE|COMPILED|PRE_RELEASED|RELEASED|CANCELED|COMMENTED")]
    public string? Status { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool DeletedOnly { get; set; }
    public bool ExcludeReleased { get; set; }
}

public sealed class SaveProjectVersionRequest
{
    [Required]
    public DateTime ProjectDate { get; set; }

    [Required, StringLength(50)]
    public string ProjectType { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Developer { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ProgramGroup { get; set; } = string.Empty;

    [StringLength(100)]
    public string? PersonnelGroup { get; set; }

    [Required, StringLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ProgramVersion { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Compiler { get; set; }

    public DateTime? CompileTime { get; set; }

    [StringLength(200)]
    public string? CompiledProgramVersion { get; set; }

    [StringLength(2000)]
    public string? VersionDescription { get; set; }

    public DateTime? ReleaseTime { get; set; }

    [StringLength(300)]
    public string? BranchName { get; set; }

    [StringLength(2000)]
    public string? Remark1 { get; set; }

    [StringLength(2000)]
    public string? Remark2 { get; set; }

    [Required, RegularExpression("PENDING|WAIT_COMPILE|COMPILED|PRE_RELEASED|RELEASED|CANCELED|COMMENTED")]
    public string Status { get; set; } = "PENDING";

    public long RowVersion { get; set; }
}

public sealed record ProjectVersionDto(
    string Id,
    DateTime ProjectDate,
    string ProjectType,
    string Developer,
    string ProgramGroup,
    string? PersonnelGroup,
    string ProgramName,
    string ProgramVersion,
    string? Compiler,
    DateTime? CompileTime,
    string? CompiledProgramVersion,
    string? VersionDescription,
    DateTime? ReleaseTime,
    string? BranchName,
    string? Remark1,
    string? Remark2,
    string Status,
    string CreatedBy,
    DateTime CreatedAt,
    string UpdatedBy,
    DateTime UpdatedAt,
    bool IsDeleted,
    long RowVersion);

public sealed record ProjectVersionStatusCountsDto(
    int Total,
    int Pending,
    int WaitCompile,
    int Compiled,
    int PreReleased,
    int Released,
    int Commented);

public sealed record ProjectVersionImportResultDto(
    int TotalRows,
    int CreatedCount,
    int SkippedDuplicateCount);

public sealed class BatchVersionMutationRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public List<VersionMutationItem> Items { get; set; } = [];
}

public sealed class VersionMutationItem
{
    [Required, StringLength(36, MinimumLength = 1)]
    public string Id { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long RowVersion { get; set; }
}
