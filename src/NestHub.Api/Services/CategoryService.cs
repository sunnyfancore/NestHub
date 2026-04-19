using FreeSql;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class CategoryService
{
    private readonly IFreeSql _orm;

    public CategoryService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<PortalCategoryDto> CreateAsync(SavePortalCategoryRequest request, TenantContext tenantContext)
    {
        var parent = await ValidateParentAsync(request.ParentId, null);
        var normalizedName = request.Name.Trim();

        var exists = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.ParentId == request.ParentId && item.Name == normalizedName)
            .AnyAsync();

        if (exists)
        {
            throw new InvalidOperationException("同级分类中已存在相同名称。");
        }

        var category = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            ParentId = request.ParentId,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orm.Insert(category).ExecuteAffrowsAsync();

        return MapCategory(category);
    }

    public async Task<PortalCategoryDto> UpdateAsync(Guid id, SavePortalCategoryRequest request)
    {
        var category = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == id).ToOneAsync();
        if (category is null)
        {
            throw new KeyNotFoundException("分类不存在。");
        }

        var parent = await ValidateParentAsync(request.ParentId, id);
        var normalizedName = request.Name.Trim();
        var exists = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id != id && item.ParentId == request.ParentId && item.Name == normalizedName)
            .AnyAsync();

        if (exists)
        {
            throw new InvalidOperationException("同级分类中已存在相同名称。");
        }

        category.Name = normalizedName;
        category.ParentId = request.ParentId;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = DateTime.UtcNow;

        await _orm.Update<Folder>().SetSource(category).ExecuteAffrowsAsync();
        return MapCategory(category);
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == id).ToOneAsync();
        if (category is null)
        {
            throw new KeyNotFoundException("分类不存在。");
        }

        var childCount = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.ParentId == id).CountAsync();
        if (childCount > 0)
        {
            throw new InvalidOperationException("请先删除子分类。");
        }

        var linkCount = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.FolderId == id).CountAsync();
        if (linkCount > 0)
        {
            throw new InvalidOperationException("请先删除或迁移该分类下的链接。");
        }

        await _orm.Delete<Folder>().Where(item => item.Id == id).ExecuteAffrowsAsync();
    }

    public async Task UpdateOrderAsync(IReadOnlyCollection<Guid> orderedIds)
    {
        if (orderedIds.Count == 0)
        {
            return;
        }

        var ids = orderedIds.Distinct().ToList();
        var categories = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => ids.Contains(item.Id)).ToListAsync();
        if (categories.Count != ids.Count)
        {
            throw new InvalidOperationException("存在无效的分类排序数据。");
        }

        var parentIds = categories.Select(item => item.ParentId ?? Guid.Empty).Distinct().ToList();
        if (parentIds.Count > 1)
        {
            throw new InvalidOperationException("只能对同级分类进行排序。");
        }

        var now = DateTime.UtcNow;
        var total = ids.Count;
        for (var index = 0; index < ids.Count; index++)
        {
            var category = categories.First(item => item.Id == ids[index]);
            category.SortOrder = (total - index) * 10;
            category.UpdatedAt = now;
        }

        await _orm.Update<Folder>().SetSource(categories).ExecuteAffrowsAsync();
    }

    private async Task<Folder?> ValidateParentAsync(Guid? parentId, Guid? selfId)
    {
        if (!parentId.HasValue)
        {
            return null;
        }

        if (selfId.HasValue && parentId.Value == selfId.Value)
        {
            throw new InvalidOperationException("分类不能设置自己为父级。");
        }

        var parent = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == parentId.Value).ToOneAsync();
        if (parent is null)
        {
            throw new InvalidOperationException("父级分类不存在。");
        }

        if (parent.ParentId.HasValue)
        {
            throw new InvalidOperationException("仅允许选择顶级分类作为父级。");
        }

        return parent;
    }

    private static PortalCategoryDto MapCategory(Folder category)
    {
        return new PortalCategoryDto
        {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            SortOrder = category.SortOrder
        };
    }
}
