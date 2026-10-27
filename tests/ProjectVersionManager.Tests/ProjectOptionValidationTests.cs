using System.ComponentModel.DataAnnotations;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Tests;

public sealed class ProjectOptionValidationTests
{
    [Fact]
    public void SaveOption_AcceptsEverySupportedOptionType()
    {
        foreach (var optionType in ProjectOptionTypes.All)
        {
            var request = new SaveProjectOptionRequest
            {
                OptionType = optionType,
                OptionValue = "测试选项",
                SortOrder = 10
            };

            Assert.Empty(Validate(request));
        }
    }

    [Fact]
    public void SaveOption_RejectsUnsupportedOptionType()
    {
        var request = new SaveProjectOptionRequest
        {
            OptionType = "UNKNOWN",
            OptionValue = "测试选项"
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.OptionType)));
    }

    [Fact]
    public void SaveOption_RejectsBlankOptionValue()
    {
        var request = new SaveProjectOptionRequest
        {
            OptionType = ProjectOptionTypes.ProgramName,
            OptionValue = string.Empty
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.OptionValue)));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }
}
