using System.ComponentModel.DataAnnotations;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Tests;

public sealed class ProjectVersionValidationTests
{
    [Fact]
    public void Apply_NormalizesRequiredTextAndOptionalBlankValues()
    {
        var request = new SaveProjectVersionRequest
        {
            ProjectDate = new DateTime(2026, 7, 30, 14, 30, 0),
            ProjectType = " SR ",
            Developer = " Xin Wang ",
            ProgramGroup = " FTWIP ",
            ProgramName = " frmSetup ",
            ProgramVersion = " 2.95 ",
            Compiler = " ",
            Status = "released"
        };
        var entity = new ProjectVersion();

        request.Apply(entity);

        Assert.Equal(new DateTime(2026, 7, 30), entity.ProjectDate);
        Assert.Equal("SR", entity.ProjectType);
        Assert.Equal("Xin Wang", entity.Developer);
        Assert.Equal("FTWIP", entity.ProgramGroup);
        Assert.Equal("frmSetup", entity.ProgramName);
        Assert.Equal("2.95", entity.ProgramVersionNumber);
        Assert.Null(entity.Compiler);
        Assert.Equal("RELEASED", entity.Status);
    }

    [Fact]
    public void BatchMutation_RequiresAtLeastOneSelectedRecord()
    {
        var request = new BatchVersionMutationRequest();

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Items)));
    }

    [Fact]
    public void VersionMutation_RequiresPositiveRowVersion()
    {
        var item = new VersionMutationItem { Id = Guid.NewGuid().ToString(), RowVersion = 0 };

        var results = Validate(item);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(item.RowVersion)));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("WAIT_COMPILE")]
    [InlineData("COMPILED")]
    [InlineData("PRE_RELEASED")]
    [InlineData("RELEASED")]
    [InlineData("CANCELED")]
    [InlineData("COMMENTED")]
    public void SaveVersion_AcceptsSupportedStatus(string status)
    {
        var request = ValidRequest(status);

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void SaveVersion_RejectsRemovedArchivedStatus()
    {
        var request = ValidRequest("ARCHIVED");

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Status)));
    }

    [Fact]
    public void VersionQuery_RejectsRemovedArchivedStatus()
    {
        var query = new ProjectVersionQuery { Status = "ARCHIVED" };

        var results = Validate(query);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(query.Status)));
    }

    private static SaveProjectVersionRequest ValidRequest(string status) =>
        new()
        {
            ProjectDate = new DateTime(2026, 7, 31),
            ProjectType = "SR",
            Developer = "Developer",
            ProgramGroup = "Group",
            ProgramName = "Program",
            ProgramVersion = "1.0.0",
            Status = status
        };

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
