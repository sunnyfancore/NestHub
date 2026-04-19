using FreeSql;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class SiteSettingService
{
    private readonly IFreeSql _orm;

    public SiteSettingService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<PortalSiteDto> GetAsync(TenantContext tenantContext)
    {
        var tenant = await _orm.Select<Tenant>().Where(item => item.Id == tenantContext.TenantId).ToOneAsync();
        if (tenant is null)
        {
            throw new KeyNotFoundException("租户不存在。");
        }

        var siteSetting = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.UpdatedAt)
            .OrderByDescending(item => item.CreatedAt)
            .ToOneAsync();
        return MapSite(siteSetting, tenant);
    }

    public async Task<PortalSiteDto> UpdateAsync(SaveSiteSettingsRequest request, TenantContext tenantContext)
    {
        var tenant = await _orm.Select<Tenant>().Where(item => item.Id == tenantContext.TenantId).ToOneAsync();
        if (tenant is null)
        {
            throw new KeyNotFoundException("租户不存在。");
        }

        var siteSetting = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.UpdatedAt)
            .OrderByDescending(item => item.CreatedAt)
            .ToOneAsync();
        if (siteSetting is null)
        {
            siteSetting = new SiteSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _orm.Insert(siteSetting).ExecuteAffrowsAsync();
        }

        siteSetting.Title = request.Title.Trim();
        siteSetting.Subtitle = request.Subtitle?.Trim();
        siteSetting.Description = request.Description?.Trim();
        siteSetting.LogoText = request.LogoText?.Trim();
        siteSetting.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        siteSetting.SearchPlaceholder = request.SearchPlaceholder?.Trim();
        siteSetting.FooterText = request.FooterText?.Trim();
        siteSetting.ThemeName = string.IsNullOrWhiteSpace(request.ThemeName) ? null : request.ThemeName.Trim();
        siteSetting.MobileThemeName = string.IsNullOrWhiteSpace(request.MobileThemeName) ? null : request.MobileThemeName.Trim();
        siteSetting.LogoMode = string.IsNullOrWhiteSpace(request.LogoMode) ? null : request.LogoMode.Trim();
        siteSetting.ShowBottomDock = request.ShowBottomDock ?? true;
        siteSetting.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .SetSource(siteSetting)
            .ExecuteAffrowsAsync();
        return MapSite(siteSetting, tenant);
    }

    public static PortalSiteDto MapSite(SiteSetting? siteSetting, Tenant tenant)
    {
        return new PortalSiteDto
        {
            Title = string.IsNullOrEmpty(siteSetting?.Title) ? tenant.Name : siteSetting.Title,
            Subtitle = siteSetting?.Subtitle ?? "一个精致的导航门户。",
            Description = siteSetting?.Description ?? "支持多租户、分类管理、私有链接与前台编辑的导航站解决方案。",
            LogoText = string.IsNullOrEmpty(siteSetting?.LogoText) ? "N" : siteSetting.LogoText,
            LogoUrl = siteSetting?.LogoUrl,
            SearchPlaceholder = siteSetting?.SearchPlaceholder ?? "搜索链接、书签或分类关键词...",
            FooterText = siteSetting?.FooterText ?? $"由 {tenant.Name} 驱动",
            ThemeName = siteSetting?.ThemeName ?? "default2",
            MobileThemeName = siteSetting?.MobileThemeName ?? siteSetting?.ThemeName ?? "default2",
            DefaultSearchEngine = siteSetting?.DefaultSearchEngine,
            LogoMode = siteSetting?.LogoMode ?? "compact",
            ShowBottomDock = siteSetting?.ShowBottomDock ?? true
        };
    }

    public async Task UpdateSearchEngineAsync(string searchEngine, TenantContext tenantContext)
    {
        var siteSetting = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.UpdatedAt)
            .OrderByDescending(item => item.CreatedAt)
            .ToOneAsync();
        if (siteSetting is null)
        {
            siteSetting = new SiteSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                Title = tenantContext.TenantName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DefaultSearchEngine = searchEngine
            };
            await _orm.Insert(siteSetting).ExecuteAffrowsAsync();
            return;
        }

        siteSetting.DefaultSearchEngine = searchEngine;
        siteSetting.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .SetSource(siteSetting)
            .ExecuteAffrowsAsync();
    }

    public async Task UpdateLogoUrlAsync(TenantContext tenantContext, string logoUrl)
    {
        var siteSetting = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.UpdatedAt)
            .OrderByDescending(item => item.CreatedAt)
            .ToOneAsync();
        if (siteSetting is null)
        {
            siteSetting = new SiteSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                Title = tenantContext.TenantName,
                LogoUrl = logoUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _orm.Insert(siteSetting).ExecuteAffrowsAsync();
            return;
        }

        siteSetting.LogoUrl = logoUrl;
        siteSetting.UpdatedAt = DateTime.UtcNow;
        await _orm.Update<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .SetSource(siteSetting)
            .ExecuteAffrowsAsync();
    }
}
