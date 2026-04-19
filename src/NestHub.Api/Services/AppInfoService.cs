using System.Reflection;
using FreeSql;
using NestHub.Api.Contracts.App;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class AppInfoService
{
    private readonly IFreeSql _orm;

    public AppInfoService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<AppInfoDto> GetAsync(TenantContext tenantContext)
    {
        var tenant = await _orm.Select<Tenant>().Where(item => item.Id == tenantContext.TenantId).ToOneAsync();
        if (tenant is null)
        {
            throw new KeyNotFoundException("租户不存在。");
        }

        var categoryCount = await _orm.Select<Folder>().CountAsync();
        var linkCount = await _orm.Select<Bookmark>().CountAsync();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        return new AppInfoDto
        {
            RuntimeVersion = Environment.Version.ToString(),
            AppVersion = version,
            CategoryCount = (int)categoryCount,
            LinkCount = (int)linkCount,
            TenantName = tenant.Name
        };
    }
}
