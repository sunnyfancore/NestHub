using FreeSql;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class NavigationLinkService
{
    private readonly IFreeSql _orm;

    public NavigationLinkService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<PortalLinkDto> CreateAsync(SavePortalLinkRequest request, TenantContext tenantContext)
    {
        var category = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == request.CategoryId && item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (category is null)
        {
            throw new InvalidOperationException("所属分类不存在。");
        }

        var link = new Bookmark
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            FolderId = request.CategoryId,
            Title = request.Title.Trim(),
            Url = request.Url.Trim(),
            StandbyUrl = string.IsNullOrWhiteSpace(request.StandbyUrl) ? null : request.StandbyUrl.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim(),
            FontIcon = string.IsNullOrWhiteSpace(request.FontIcon) ? null : request.FontIcon.Trim(),
            IsPinned = request.IsPinned,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orm.Insert(link).ExecuteAffrowsAsync();
        return MapLink(link, category.Name);
    }

    public async Task<PortalLinkDto> UpdateAsync(Guid id, SavePortalLinkRequest request, TenantContext tenantContext)
    {
        var link = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == id && item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (link is null)
        {
            throw new KeyNotFoundException("链接不存在。");
        }

        var category = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == request.CategoryId && item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (category is null)
        {
            throw new InvalidOperationException("所属分类不存在。");
        }

        link.FolderId = request.CategoryId;
        link.Title = request.Title.Trim();
        link.Url = request.Url.Trim();
        link.StandbyUrl = string.IsNullOrWhiteSpace(request.StandbyUrl) ? null : request.StandbyUrl.Trim();
        link.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        link.Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim();
        link.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        link.FontIcon = string.IsNullOrWhiteSpace(request.FontIcon) ? null : request.FontIcon.Trim();
        link.IsPinned = request.IsPinned;
        link.SortOrder = request.SortOrder;
        link.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Bookmark>().DisableGlobalFilter("TenantFilter").SetSource(link).ExecuteAffrowsAsync();
        return MapLink(link, category.Name);
    }

    public async Task DeleteAsync(Guid id, TenantContext tenantContext)
    {
        var deleted = await _orm.Delete<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == id && item.TenantId == tenantContext.TenantId).ExecuteAffrowsAsync();
        if (deleted == 0)
        {
            throw new KeyNotFoundException("链接不存在。");
        }
    }

    public async Task UpdateOrderAsync(IReadOnlyCollection<Guid> orderedIds, TenantContext tenantContext)
    {
        if (orderedIds.Count == 0)
        {
            return;
        }

        var ids = orderedIds.Distinct().ToList();
        var links = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId && ids.Contains(item.Id)).ToListAsync();
        if (links.Count != ids.Count)
        {
            throw new InvalidOperationException("存在无效的链接排序数据。");
        }

        var categoryIds = links.Select(item => item.FolderId ?? Guid.Empty).Distinct().ToList();
        if (categoryIds.Count > 1)
        {
            throw new InvalidOperationException("只能对同一分类下的链接进行排序。");
        }

        var now = DateTime.UtcNow;
        var total = ids.Count;
        for (var index = 0; index < ids.Count; index++)
        {
            var link = links.First(item => item.Id == ids[index]);
            link.SortOrder = (total - index) * 10;
            link.UpdatedAt = now;
        }

        await _orm.Update<Bookmark>().DisableGlobalFilter("TenantFilter").SetSource(links).ExecuteAffrowsAsync();
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
