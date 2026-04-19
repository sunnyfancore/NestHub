using FreeSql;
using NestHub.Api.Contracts.Bookmarks;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class BookmarkService
{
    private readonly IFreeSql _orm;

    public BookmarkService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<BookmarkListResponse> GetPagedListAsync(BookmarkQueryRequest request)
    {
        var query = _orm.Select<Bookmark>();
        var keyword = request.Keyword?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.Title.Contains(keyword) ||
                item.Url.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)) ||
                (item.Tags != null && item.Tags.Contains(keyword)));
        }

        if (request.FolderId.HasValue)
        {
            query = query.Where(item => item.FolderId == request.FolderId.Value);
        }

        var total = await query.CountAsync();
        var bookmarks = await query
            .OrderByDescending(item => item.IsPinned)
            .OrderBy(item => item.SortOrder)
            .Page(request.Page, request.PageSize)
            .ToListAsync();

        var folders = await _orm.Select<Folder>().OrderBy(item => item.SortOrder).ToListAsync();
        var folderMap = folders.ToDictionary(item => item.Id, item => item.Name);

        return new BookmarkListResponse
        {
            Items = bookmarks.Select(item => new BookmarkDto
            {
                Id = item.Id,
                FolderId = item.FolderId,
                FolderName = item.FolderId.HasValue ? folderMap.GetValueOrDefault(item.FolderId.Value) : null,
                Title = item.Title,
                Url = item.Url,
                Description = item.Description,
                Tags = item.Tags,
                IconUrl = item.IconUrl,
                IsPinned = item.IsPinned,
                SortOrder = item.SortOrder,
                ClickCount = item.ClickCount,
                LastVisitedAt = item.LastVisitedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            Folders = folders.Select(item => new BookmarkFolderOptionDto
            {
                Id = item.Id,
                Name = item.Name
            }).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            Total = total
        };
    }

    public async Task<BookmarkDto> CreateAsync(SaveBookmarkRequest request, TenantContext tenantContext)
    {
        if (request.FolderId.HasValue)
        {
            var folderExists = await _orm.Select<Folder>().Where(item => item.Id == request.FolderId.Value).AnyAsync();
            if (!folderExists)
            {
                throw new InvalidOperationException("选中的目录不存在。");
            }
        }

        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            FolderId = request.FolderId,
            Title = request.Title.Trim(),
            Url = request.Url.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim(),
            IsPinned = request.IsPinned,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orm.Insert(bookmark).ExecuteAffrowsAsync();

        return await GetAsync(bookmark.Id);
    }

    public async Task<BookmarkDto> UpdateAsync(Guid id, SaveBookmarkRequest request)
    {
        var bookmark = await _orm.Select<Bookmark>().Where(item => item.Id == id).ToOneAsync();
        if (bookmark is null)
        {
            throw new KeyNotFoundException("书签不存在。");
        }

        if (request.FolderId.HasValue)
        {
            var folderExists = await _orm.Select<Folder>().Where(item => item.Id == request.FolderId.Value).AnyAsync();
            if (!folderExists)
            {
                throw new InvalidOperationException("选中的目录不存在。");
            }
        }

        bookmark.FolderId = request.FolderId;
        bookmark.Title = request.Title.Trim();
        bookmark.Url = request.Url.Trim();
        bookmark.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        bookmark.Tags = string.IsNullOrWhiteSpace(request.Tags) ? null : request.Tags.Trim();
        bookmark.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        bookmark.IsPinned = request.IsPinned;
        bookmark.SortOrder = request.SortOrder;
        bookmark.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Bookmark>().SetSource(bookmark).ExecuteAffrowsAsync();

        return await GetAsync(bookmark.Id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var deleted = await _orm.Delete<Bookmark>().Where(item => item.Id == id).ExecuteAffrowsAsync();
        if (deleted == 0)
        {
            throw new KeyNotFoundException("书签不存在。");
        }
    }

    public async Task OpenAsync(Guid id)
    {
        var bookmark = await _orm.Select<Bookmark>().Where(item => item.Id == id).ToOneAsync();
        if (bookmark is null)
        {
            throw new KeyNotFoundException("书签不存在。");
        }

        bookmark.ClickCount += 1;
        bookmark.LastVisitedAt = DateTime.UtcNow;
        bookmark.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Bookmark>().SetSource(bookmark).ExecuteAffrowsAsync();
    }

    private async Task<BookmarkDto> GetAsync(Guid id)
    {
        var bookmark = await _orm.Select<Bookmark>().Where(item => item.Id == id).ToOneAsync();
        if (bookmark is null)
        {
            throw new KeyNotFoundException("书签不存在。");
        }

        var folderName = bookmark.FolderId.HasValue
            ? (await _orm.Select<Folder>().Where(item => item.Id == bookmark.FolderId.Value).ToOneAsync())?.Name
            : null;

        return new BookmarkDto
        {
            Id = bookmark.Id,
            FolderId = bookmark.FolderId,
            FolderName = folderName,
            Title = bookmark.Title,
            Url = bookmark.Url,
            Description = bookmark.Description,
            Tags = bookmark.Tags,
            IconUrl = bookmark.IconUrl,
            IsPinned = bookmark.IsPinned,
            SortOrder = bookmark.SortOrder,
            ClickCount = bookmark.ClickCount,
            LastVisitedAt = bookmark.LastVisitedAt,
            CreatedAt = bookmark.CreatedAt,
            UpdatedAt = bookmark.UpdatedAt
        };
    }
}
