namespace NestHub.Api.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NestHub.Api";

    public string Audience { get; set; } = "NestHub.Web";

    public string SecretKey { get; set; } = "ReplaceThisWithAtLeast32CharsForNestHub!";

    public int ExpireHours { get; set; } = 24;
}
