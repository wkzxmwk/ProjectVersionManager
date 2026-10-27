using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Tests;

public sealed class PersonnelPermissionValidationTests
{
    private static readonly string[] ExpectedPermissions =
    [
        "VERSION_VIEW",
        "VERSION_CREATE",
        "VERSION_UPDATE",
        "VERSION_DELETE",
        "VERSION_IMPORT",
        "LOG_VIEW",
        "OPTION_MANAGE",
        "PROGRAM_RELATION_MANAGE",
        "PERSONNEL_MANAGE"
    ];

    [Fact]
    public void SystemPermissions_MatchTheDatabasePermissionContract()
    {
        Assert.Equal(
            ExpectedPermissions.OrderBy(x => x),
            SystemPermissions.All.OrderBy(x => x));
        Assert.Equal(
            SystemPermissions.All.Count,
            SystemPermissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(SystemPermissions.All, permission =>
        {
            Assert.True(permission.Length <= 50);
            Assert.Matches(new Regex("^[A-Z][A-Z0-9_]*$"), permission);
        });
    }

    [Fact]
    public void UpdatePermissions_AcceptsEverySupportedPermission()
    {
        var request = new UpdatePersonnelPermissionsRequest
        {
            Permissions = SystemPermissions.All.ToList()
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void UpdatePermissions_AcceptsEmptyList_ForRevokingAllPermissions()
    {
        var request = new UpdatePersonnelPermissionsRequest
        {
            Permissions = []
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void UpdatePermissions_RejectsNullList()
    {
        var request = new UpdatePersonnelPermissionsRequest
        {
            Permissions = null!
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(nameof(request.Permissions)));
    }

    [Theory]
    [InlineData("", "张三", "123456", nameof(CreatePersonnelRequest.EmployeeNumber))]
    [InlineData("A 001", "张三", "123456", nameof(CreatePersonnelRequest.EmployeeNumber))]
    [InlineData("A001", "", "123456", nameof(CreatePersonnelRequest.PersonnelName))]
    [InlineData("A001", "张三", "12345", nameof(CreatePersonnelRequest.ManagementPassword))]
    public void CreatePersonnel_RejectsInvalidIdentityOrPassword(
        string employeeNumber,
        string personnelName,
        string managementPassword,
        string invalidMember)
    {
        var request = new CreatePersonnelRequest
        {
            EmployeeNumber = employeeNumber,
            PersonnelName = personnelName,
            ManagementPassword = managementPassword
        };

        Assert.Contains(
            Validate(request),
            result => result.MemberNames.Contains(invalidMember));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
