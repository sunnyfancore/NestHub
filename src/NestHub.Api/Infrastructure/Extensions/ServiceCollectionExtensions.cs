using FreeSql;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Domain.Interfaces;
using NestHub.Api.Infrastructure.Configuration;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFreeSql(this IServiceCollection services)
    {
        services.AddSingleton<IFreeSql>(sp =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var tenantContextAccessor = sp.GetRequiredService<TenantContextAccessor>();

            var provider = dbOptions.Provider.Trim().ToLowerInvariant();
            var dataType = provider switch
            {
                "mysql" => DataType.MySql,
                "sqlite" => DataType.Sqlite,
                _ => throw new InvalidOperationException("Database:Provider must be MySql or Sqlite.")
            };
            var connectionString = dbOptions.ConnectionString;
            if (dataType == DataType.Sqlite)
            {
                var connection = new SqliteConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource == ":memory:")
                    throw new InvalidOperationException("SQLite requires a persistent Data Source file.");
                var root = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
                connection.DataSource = Path.GetFullPath(connection.DataSource, root);
                Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
                connection.DefaultTimeout = 30;
                connectionString = connection.ConnectionString;
            }

            var orm = new FreeSqlBuilder()
                .UseConnectionString(dataType, connectionString)
                .UseAutoSyncStructure(dbOptions.AutoSyncStructure)
                .Build();

            orm.Aop.AuditValue += (_, eventArgs) =>
            {
                if (eventArgs.Property.Name == nameof(AuditableEntity.CreatedAt)
                    && eventArgs.Value is DateTime createdAt
                    && createdAt == default)
                {
                    eventArgs.Value = DateTime.UtcNow;
                }

                if (eventArgs.Property.Name == nameof(AuditableEntity.UpdatedAt))
                {
                    eventArgs.Value = DateTime.UtcNow;
                }

                if (eventArgs.Object is ITenantEntity
                    && eventArgs.Property.Name == nameof(ITenantEntity.TenantId)
                    && eventArgs.Value is Guid tenantIdValue
                    && tenantIdValue == Guid.Empty
                    && tenantContextAccessor.Current is not null)
                {
                    eventArgs.Value = tenantContextAccessor.Current.TenantId;
                }
            };

            orm.GlobalFilter.ApplyIf<ITenantEntity>(
                "TenantFilter",
                () => tenantContextAccessor.Current is not null,
                entity => entity.TenantId == tenantContextAccessor.Current!.TenantId);

            return orm;
        });

        return services;
    }
}
