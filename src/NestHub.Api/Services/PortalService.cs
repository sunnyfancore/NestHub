using FreeSql;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class PortalService
{
    private readonly IFreeSql _orm;

    public PortalService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<PortalResponse> GetPortalAsync(TenantContext? viewer, bool? publicView = null)
    {
        var tenant = await ResolveTenantAsync(viewer, publicView);
        if (tenant is null)
        {
            throw new KeyNotFoundException("当前没有可展示的租户。");
        }

        var isPublic = tenant.Id == Tenant.PublicTenantId;
        var canEdit = isPublic
            ? viewer?.IsSuperAdmin == true
            : viewer is not null;
        var siteSetting = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenant.Id)
            .ToOneAsync();

        var categoryQuery = _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenant.Id);

        var categories = await categoryQuery
            .OrderByDescending(item => item.SortOrder)
            .OrderBy(item => item.Name)
            .ToListAsync();

        var categoryIds = categories.Select(item => item.Id).ToList();
        var links = categoryIds.Count == 0
            ? []
            : await BuildLinkQuery(categoryIds, tenant.Id, canEdit)
                .OrderByDescending(item => item.IsPinned)
                .OrderByDescending(item => item.SortOrder)
                .OrderBy(item => item.Title)
                .ToListAsync();

        var categoryMap = categories.ToDictionary(item => item.Id);
        var linksByCategory = links
            .Where(item => item.FolderId.HasValue)
            .GroupBy(item => item.FolderId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<PortalLinkDto>)group.Select(item => MapLink(item, categoryMap.GetValueOrDefault(group.Key)?.Name ?? string.Empty)).ToList());

        var childCategoryMap = categories
            .Where(item => item.ParentId.HasValue)
            .GroupBy(item => item.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.SortOrder)
                    .ThenBy(item => item.Name)
                    .ToList());

        var rootCategories = categories
            .Where(item => !item.ParentId.HasValue)
            .OrderByDescending(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        var categoryDtos = rootCategories
            .Select(item => BuildCategoryTree(item, childCategoryMap, linksByCategory, canEdit))
            .Where(item => item is not null)
            .Cast<PortalCategoryDto>()
            .ToList();

        var featuredLinks = links
            .Where(item => item.IsPinned)
            .Take(8)
            .Select(item =>
            {
                var categoryName = item.FolderId.HasValue && categoryMap.TryGetValue(item.FolderId.Value, out var category)
                    ? category.Name
                    : string.Empty;

                return MapLink(item, categoryName);
            })
            .ToList();

        return new PortalResponse
        {
            Site = MapSite(siteSetting, tenant),
            Tenant = new PortalTenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name
            },
            IsAuthenticated = viewer is not null,
            CanEdit = canEdit,
            FeaturedLinks = featuredLinks,
            Categories = categoryDtos,
            Editor = canEdit
                ? new PortalEditorDto
                {
                    CategoryOptions = categories
                        .Select(item => new PortalCategoryOptionDto
                        {
                            Id = item.Id,
                            ParentId = item.ParentId,
                            Name = item.Name,
                            Level = item.ParentId.HasValue ? 2 : 1
                        })
                        .ToList()
                }
                : null
        };
    }

    public async Task<IReadOnlyCollection<PortalSearchResultDto>> SearchAsync(string keyword, TenantContext? viewer, bool? publicView = null)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var tenant = await ResolveTenantAsync(viewer, publicView);
        if (tenant is null)
        {
            return [];
        }

        var isPublic = tenant.Id == Tenant.PublicTenantId;
        var canEdit = isPublic
            ? viewer?.IsSuperAdmin == true
            : viewer is not null;
        var categories = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenant.Id)
            .ToListAsync();

        var categoryIds = categories
            .Select(item => item.Id)
            .ToList();

        if (categoryIds.Count == 0)
        {
            return [];
        }

        var query = BuildLinkQuery(categoryIds, tenant.Id, canEdit);

        keyword = keyword.Trim();
        var links = await query
            .Where(item =>
                item.Title.Contains(keyword) ||
                item.Url.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)) ||
                (item.Tags != null && item.Tags.Contains(keyword)))
            .OrderByDescending(item => item.IsPinned)
            .OrderByDescending(item => item.SortOrder)
            .Take(18)
            .ToListAsync();

        var categoryMap = categories.ToDictionary(item => item.Id, item => item.Name);

        return links.Select(item => new PortalSearchResultDto
        {
            Id = item.Id,
            Title = item.Title,
            Url = item.Url,
            StandbyUrl = item.StandbyUrl,
            Description = item.Description,
            IconUrl = item.IconUrl,
            CategoryName = item.FolderId.HasValue ? categoryMap.GetValueOrDefault(item.FolderId.Value, string.Empty) : string.Empty
        }).ToList();
    }

    public async Task RecordClickAsync(Guid linkId, TenantContext? viewer)
    {
        var link = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == linkId)
            .ToOneAsync();

        if (link is null)
        {
            throw new KeyNotFoundException("链接不存在。");
        }

        link.ClickCount += 1;
        link.LastVisitedAt = DateTime.UtcNow;
        link.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .SetSource(link)
            .ExecuteAffrowsAsync();
    }

    private async Task<Tenant?> ResolveTenantAsync(TenantContext? viewer, bool? publicView = null)
    {
        if (publicView == true)
        {
            return await _orm.Select<Tenant>().Where(item => item.Id == Tenant.PublicTenantId).ToOneAsync();
        }

        if (viewer is not null)
        {
            return await _orm.Select<Tenant>().Where(item => item.Id == viewer.TenantId).ToOneAsync();
        }

        return await _orm.Select<Tenant>().Where(item => item.Id == Tenant.PublicTenantId).ToOneAsync();
    }

    private ISelect<Bookmark> BuildLinkQuery(IReadOnlyCollection<Guid> categoryIds, Guid tenantId, bool canEdit)
    {
        var query = _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantId)
            .Where(item => item.FolderId.HasValue && categoryIds.Contains(item.FolderId.Value));

        return query;
    }

    private PortalCategoryDto? BuildCategoryTree(
        Folder category,
        IReadOnlyDictionary<Guid, List<Folder>> childCategoryMap,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<PortalLinkDto>> linksByCategory,
        bool includeEmpty)
    {
        childCategoryMap.TryGetValue(category.Id, out var children);
        var childDtos = children?
            .Select(item => BuildCategoryTree(item, childCategoryMap, linksByCategory, includeEmpty))
            .Where(item => item is not null)
            .Cast<PortalCategoryDto>()
            .ToList() ?? [];

        var links = linksByCategory.GetValueOrDefault(category.Id, []);
        if (!includeEmpty && childDtos.Count == 0 && links.Count == 0)
        {
            return null;
        }

        return new PortalCategoryDto
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            SortOrder = category.SortOrder,
            Links = links,
            Children = childDtos
        };
    }

    private static PortalSiteDto MapSite(SiteSetting? siteSetting, Tenant tenant)
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
            LogoMode = siteSetting?.LogoMode ?? "compact"
        };
    }

    private static PortalLinkDto MapLink(Bookmark link, string categoryName)
    {
        return new PortalLinkDto
        {
            Id = link.Id,
            CategoryId = link.FolderId ?? Guid.Empty,
            CategoryName = categoryName,
            Title = link.Title,
            Url = link.Url,
            StandbyUrl = link.StandbyUrl,
            Description = link.Description,
            Tags = link.Tags,
            IconUrl = link.IconUrl,
            FontIcon = link.FontIcon,
            IsPinned = link.IsPinned,
            SortOrder = link.SortOrder,
            ClickCount = link.ClickCount,
            CheckStatus = link.CheckStatus,
            LastCheckedAt = link.LastCheckedAt,
            LastVisitedAt = link.LastVisitedAt
        };
    }
}
