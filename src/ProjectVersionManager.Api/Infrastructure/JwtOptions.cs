namespace ProjectVersionManager.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ProjectVersionManager";
    public string Audience { get; set; } = "ProjectVersionManager.Web";
    public string Key { get; set; } = string.Empty;
    public int ExpireMinutes { get; set; } = 480;
}
