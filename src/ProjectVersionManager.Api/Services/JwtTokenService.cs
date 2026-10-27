using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;
using ProjectVersionManager.Api.Infrastructure;

namespace ProjectVersionManager.Api.Services;

public interface IJwtTokenService
{
    LoginResponse Create(PvmUser user, IReadOnlyCollection<string> permissions);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public LoginResponse Create(PvmUser user, IReadOnlyCollection<string> permissions)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpireMinutes);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("display_name", user.DisplayName ?? user.Username),
            new Claim(ClaimTypes.Role, user.RoleCode),
            new Claim("staff_group", user.StaffGroup ?? string.Empty),
            new Claim("personnel_group", user.PersonnelGroup ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(permissions.Select(permission =>
            new Claim(SystemPermissions.ClaimType, permission)));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            user.Username,
            user.DisplayName ?? user.Username,
            user.RoleCode,
            user.StaffGroup,
            user.PersonnelGroup,
            permissions.OrderBy(x => x).ToList());
    }
}
