using System.ComponentModel.DataAnnotations;

namespace ProjectVersionManager.Api.Dtos;

public sealed record ProjectOptionDto(
    string Id,
    string OptionType,
    string OptionValue,
    string? StaffGroup,
    string? PersonnelGroup,
    string? ProgramGroup,
    int SortOrder,
    string UpdatedBy,
    DateTime UpdatedAt,
    long RowVersion);

public sealed record ProjectOptionImportResultDto(
    int TotalRows,
    int CreatedCount,
    int SkippedDuplicateCount);

public sealed class SaveProjectOptionRequest
{
    [Required, RegularExpression("PROJECT_TYPE|STAFF_GROUP|PERSONNEL_GROUP|DEVELOPER|PROGRAM_GROUP|PROGRAM_NAME|COMPILER")]
    public string OptionType { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string OptionValue { get; set; } = string.Empty;

    [StringLength(100)]
    public string? StaffGroup { get; set; }

    [StringLength(100)]
    public string? PersonnelGroup { get; set; }

    [StringLength(100)]
    public string? ProgramGroup { get; set; }

    [Range(0, 999999)]
    public int SortOrder { get; set; }

    public long RowVersion { get; set; }
}
