using FreeSql;
using Microsoft.Extensions.Options;
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

            var orm = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, dbOptions.ConnectionString)
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
