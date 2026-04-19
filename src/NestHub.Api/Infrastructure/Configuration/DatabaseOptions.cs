namespace NestHub.Api.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    public bool AutoSyncStructure { get; set; }
}
