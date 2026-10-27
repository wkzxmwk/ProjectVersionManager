using System.ComponentModel.DataAnnotations;

namespace ProjectVersionManager.Api.Dtos;

public sealed class CreatePersonnelRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "操作人员工号只能包含字母、数字、点、下划线和短横线。")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string PersonnelName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string ManagementPassword { get; set; } = string.Empty;

    [StringLength(100)]
    public string? StaffGroup { get; set; }

    [StringLength(100)]
    public string? PersonnelGroup { get; set; }
}

public sealed class UpdatePersonnelRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "操作人员工号只能包含字母、数字、点、下划线和短横线。")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string PersonnelName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? StaffGroup { get; set; }

    [StringLength(100)]
    public string? PersonnelGroup { get; set; }
}

public sealed class ResetPersonnelPasswordRequest
{
    [Required, StringLength(100, MinimumLength = 6)]
    public string ManagementPassword { get; set; } = string.Empty;
}

public sealed class UpdatePersonnelPermissionsRequest
{
    [Required]
    public List<string> Permissions { get; set; } = [];
}

public sealed record PersonnelDto(
    string Id,
    string EmployeeNumber,
    string PersonnelName,
    string? StaffGroup,
    string? PersonnelGroup,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> Permissions);
