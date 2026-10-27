using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;
using ProjectVersionManager.Api.Services;

namespace ProjectVersionManager.Tests;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions TestOptions = new()
    {
        Issuer = "project-version-manager-tests",
        Audience = "project-version-manager-tests",
        Key = "test-only-signing-key-that-is-long-enough-for-hmac-sha256",
        ExpireMinutes = 30
    };

    [Fact]
    public void Create_IncludesPersonnelIdentityAndAdministratorRole()
    {
        var user = new PvmUser
        {
            Id = "user-001",
            Username = "A001",
            DisplayName = "张三",
            RoleCode = "ADMIN"
        };

        var response = CreateService().Create(user, SystemPermissions.All);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

        Assert.Equal("A001", response.Username);
        Assert.Equal("张三", response.DisplayName);
        Assert.Equal("ADMIN", response.RoleCode);
        Assert.Equal("user-001", token.Subject);
        Assert.Equal("A001", Claim(token, ClaimTypes.Name));
        Assert.Equal("张三", Claim(token, "display_name"));
        Assert.Equal("ADMIN", Claim(token, ClaimTypes.Role));
        Assert.Equal(
            SystemPermissions.All.OrderBy(x => x),
            response.Permissions);
        Assert.Equal(
            SystemPermissions.All.OrderBy(x => x),
            Claims(token, SystemPermissions.ClaimType).OrderBy(x => x));
    }

    [Fact]
    public void Create_WhenPersonnelNameIsMissing_FallsBackToEmployeeNumber()
    {
        var user = new PvmUser
        {
            Id = "user-002",
            Username = "A002",
            DisplayName = null,
            RoleCode = "USER"
        };

        var response = CreateService().Create(user, []);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

        Assert.Equal("A002", response.DisplayName);
        Assert.Equal("A002", Claim(token, "display_name"));
        Assert.Equal("USER", Claim(token, ClaimTypes.Role));
        Assert.Empty(response.Permissions);
        Assert.Empty(Claims(token, SystemPermissions.ClaimType));
    }

    private static JwtTokenService CreateService() =>
        new(Options.Create(TestOptions));

    private static string? Claim(JwtSecurityToken token, string type) =>
        token.Claims.SingleOrDefault(claim => claim.Type == type)?.Value;

    private static IEnumerable<string> Claims(JwtSecurityToken token, string type) =>
        token.Claims.Where(claim => claim.Type == type).Select(claim => claim.Value);
}
