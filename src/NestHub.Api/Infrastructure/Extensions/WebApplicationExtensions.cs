using FreeSql;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.Configuration;
using NestHub.Api.Infrastructure.Persistence;

namespace NestHub.Api.Infrastructure.Extensions;

public static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var orm = scope.ServiceProvider.GetRequiredService<IFreeSql>();

        await EnsureDatabaseCreatedAsync(dbOptions.ConnectionString);

        if (dbOptions.AutoSyncStructure)
        {
            orm.CodeFirst.SyncStructure<Tenant>();

            orm.CodeFirst.SyncStructure<SiteSetting>();
            orm.CodeFirst.SyncStructure<ShareLink>();
            orm.CodeFirst.SyncStructure<TransitionSetting>();
            orm.CodeFirst.SyncStructure<Folder>();
            orm.CodeFirst.SyncStructure<Bookmark>();
            orm.CodeFirst.SyncStructure<PasswordResetToken>();
            orm.CodeFirst.SyncStructure<SmtpSetting>();
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }

    private static async Task EnsureDatabaseCreatedAsync(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        builder.Database = string.Empty;

        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var escapedDatabaseName = databaseName.Replace("`", "``", StringComparison.Ordinal);
        var commandText =
            $"CREATE DATABASE IF NOT EXISTS `{escapedDatabaseName}` DEFAULT CHARACTER SET utf8mb4 DEFAULT COLLATE utf8mb4_unicode_ci;";

        await using var command = new MySqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
