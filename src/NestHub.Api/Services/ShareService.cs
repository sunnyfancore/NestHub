using FreeSql;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class ShareService
{
    private readonly IFreeSql _orm;

    public ShareService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<IReadOnlyCollection<ShareLinkItemDto>> GetListAsync()
    {
        var shares = await _orm.Select<ShareLink>().OrderByDescending(item => item.CreatedAt).ToListAsync();
        var categoryIds = shares.Select(item => item.CategoryId).Distinct().ToList();
        var categories = categoryIds.Count == 0
            ? []
            : await _orm.Select<Folder>().Where(item => categoryIds.Contains(item.Id)).ToListAsync();
        var categoryMap = categories.ToDictionary(item => item.Id, item => item.Name);

        return shares.Select(item => new ShareLinkItemDto
        {
            Id = item.Id,
            ShareCode = item.ShareCode,
            CategoryId = item.CategoryId,
            CategoryName = categoryMap.GetValueOrDefault(item.CategoryId, string.Empty),
            Password = item.Password,
            Note = item.Note,
            ExpireAt = item.ExpireAt,
            CreatedAt = item.CreatedAt
        }).ToList();
    }

    public async Task<ShareLinkItemDto> CreateAsync(SaveShareLinkRequest request, TenantContext tenantContext)
    {
        var category = await _orm.Select<Folder>().Where(item => item.Id == request.CategoryId).ToOneAsync();
        if (category is null)
        {
            throw new InvalidOperationException("分享分类不存在。");
        }

        var share = new ShareLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            CategoryId = request.CategoryId,
            ShareCode = Guid.NewGuid().ToString("N")[..8],
            Password = string.IsNullOrWhiteSpace(request.Password) ? null : request.Password.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ExpireAt = request.ExpireAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _orm.Insert(share).ExecuteAffrowsAsync();

        return new ShareLinkItemDto
        {
            Id = share.Id,
            ShareCode = share.ShareCode,
            CategoryId = share.CategoryId,
            CategoryName = category.Name,
            Password = share.Password,
            Note = share.Note,
            ExpireAt = share.ExpireAt,
            CreatedAt = share.CreatedAt
        };
    }

    public async Task DeleteAsync(Guid id)
    {
        var deleted = await _orm.Delete<ShareLink>().Where(item => item.Id == id).ExecuteAffrowsAsync();
        if (deleted == 0)
        {
            throw new KeyNotFoundException("分享记录不存在。");
        }
    }
}
