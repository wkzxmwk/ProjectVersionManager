using System.ComponentModel.DataAnnotations;

namespace ProjectVersionManager.Api.Dtos;

public sealed class LoginRequest
{
    [Required, StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    string Username,
    string DisplayName,
    string RoleCode,
    string? StaffGroup,
    string? PersonnelGroup,
    IReadOnlyList<string> Permissions);
