namespace NestHub.Api.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    // Existing configurations without a provider keep using MySQL.
    public string Provider { get; set; } = "MySql";

    public string ConnectionString { get; set; } = string.Empty;

    public bool AutoSyncStructure { get; set; }
}
