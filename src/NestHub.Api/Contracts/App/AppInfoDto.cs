namespace NestHub.Api.Contracts.App;

public sealed class AppInfoDto
{
    public string RuntimeVersion { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;

    public int CategoryCount { get; set; }

    public int LinkCount { get; set; }

    public string TenantName { get; set; } = string.Empty;
}
