using System.ComponentModel.DataAnnotations;
using ProjectVersionManager.Api.Dtos;

namespace ProjectVersionManager.Tests;

public sealed class AuthValidationTests
{
    [Fact]
    public void ChangePassword_AcceptsValidPasswords()
    {
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "old-password",
            NewPassword = "new-password"
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void ChangePassword_RejectsBlankCurrentPasswordAndShortNewPassword()
    {
        var request = new ChangePasswordRequest
        {
            CurrentPassword = string.Empty,
            NewPassword = "12345"
        };

        var results = Validate(request);

        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(request.CurrentPassword)));
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(request.NewPassword)));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
