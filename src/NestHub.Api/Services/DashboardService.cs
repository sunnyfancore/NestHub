using FreeSql;
using NestHub.Api.Contracts.Dashboard;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class DashboardService
{
    private readonly IFreeSql _orm;

    public DashboardService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(TenantContext tenantContext)
    {
        var bookmarkCount = await _orm.Select<Bookmark>().CountAsync();
        var folderCount = await _orm.Select<Folder>().CountAsync();
        var pinnedCount = await _orm.Select<Bookmark>().Where(item => item.IsPinned).CountAsync();

        var recentBookmarks = await _orm.Select<Bookmark>()
            .OrderByDescending(item => item.UpdatedAt)
            .Page(1, 6)
            .ToListAsync();

        var folderIds = recentBookmarks.Where(item => item.FolderId.HasValue).Select(item => item.FolderId!.Value).Distinct().ToList();
        var folders = folderIds.Count == 0
            ? []
            : await _orm.Select<Folder>().Where(item => folderIds.Contains(item.Id)).ToListAsync();
        var folderMap = folders.ToDictionary(item => item.Id, item => item.Name);

        return new DashboardSummaryDto
        {
            TenantName = tenantContext.TenantName,
            BookmarkCount = (int)bookmarkCount,
            FolderCount = (int)folderCount,
            PinnedCount = (int)pinnedCount,
            RecentBookmarks = recentBookmarks.Select(item => new RecentBookmarkDto
            {
                Id = item.Id,
                Title = item.Title,
                Url = item.Url,
                FolderName = item.FolderId.HasValue ? folderMap.GetValueOrDefault(item.FolderId.Value) : null,
                UpdatedAt = item.UpdatedAt
            }).ToList()
        };
    }
}
