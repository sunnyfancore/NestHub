using FreeSql;
using NestHub.Api.Contracts.Folders;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class FolderService
{
    private readonly IFreeSql _orm;

    public FolderService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<IReadOnlyCollection<FolderDto>> GetListAsync(TenantContext tenantContext)
    {
        var folders = await _orm.Select<Folder>()
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderBy(item => item.SortOrder)
            .OrderBy(item => item.Name)
            .ToListAsync();

        var folderIds = folders.Select(item => item.Id).ToList();
        var bookmarks = folderIds.Count == 0
            ? []
            : await _orm.Select<Bookmark>()
                .Where(item => item.TenantId == tenantContext.TenantId && item.FolderId.HasValue && folderIds.Contains(item.FolderId.Value))
                .ToListAsync();

        var bookmarkCountMap = bookmarks
            .GroupBy(item => item.FolderId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return folders.Select(item => new FolderDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            SortOrder = item.SortOrder,
            BookmarkCount = bookmarkCountMap.GetValueOrDefault(item.Id),
            CreatedAt = item.CreatedAt
        }).ToList();
    }

    public async Task<FolderDto> CreateAsync(SaveFolderRequest request, TenantContext tenantContext)
    {
        var existed = await _orm.Select<Folder>().Where(item => item.TenantId == tenantContext.TenantId && item.Name == request.Name.Trim()).AnyAsync();
        if (existed)
        {
            throw new InvalidOperationException("同一租户下已经存在同名目录。");
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orm.Insert(folder).ExecuteAffrowsAsync();

        return new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description,
            SortOrder = folder.SortOrder,
            BookmarkCount = 0,
            CreatedAt = folder.CreatedAt
        };
    }

    public async Task<FolderDto> UpdateAsync(Guid id, SaveFolderRequest request, TenantContext tenantContext)
    {
        var folder = await _orm.Select<Folder>().Where(item => item.Id == id && item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (folder is null)
        {
            throw new KeyNotFoundException("目录不存在。");
        }

        var normalizedName = request.Name.Trim();
        var existed = await _orm.Select<Folder>()
            .Where(item => item.Id != id && item.Name == normalizedName)
            .Where(item => item.TenantId == tenantContext.TenantId)
            .AnyAsync();

        if (existed)
        {
            throw new InvalidOperationException("同一租户下已经存在同名目录。");
        }

        folder.Name = normalizedName;
        folder.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        folder.SortOrder = request.SortOrder;
        folder.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Folder>().SetSource(folder).ExecuteAffrowsAsync();

        var bookmarkCount = await _orm.Select<Bookmark>().Where(item => item.TenantId == tenantContext.TenantId && item.FolderId == folder.Id).CountAsync();

        return new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description,
            SortOrder = folder.SortOrder,
            BookmarkCount = (int)bookmarkCount,
            CreatedAt = folder.CreatedAt
        };
    }

    public async Task DeleteAsync(Guid id, TenantContext tenantContext)
    {
        var folder = await _orm.Select<Folder>().Where(item => item.Id == id && item.TenantId == tenantContext.TenantId).ToOneAsync();
        if (folder is null)
        {
            throw new KeyNotFoundException("目录不存在。");
        }

        var bookmarkCount = await _orm.Select<Bookmark>().Where(item => item.TenantId == tenantContext.TenantId && item.FolderId == folder.Id).CountAsync();
        if (bookmarkCount > 0)
        {
            throw new InvalidOperationException("该目录下还有书签，请先转移或删除书签。");
        }

        await _orm.Delete<Folder>().Where(item => item.Id == id && item.TenantId == tenantContext.TenantId).ExecuteAffrowsAsync();
    }
}
