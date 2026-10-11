using FreeSql;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class ShareService
{
    private readonly IFreeSql _orm;
    private static readonly PasswordHasher<ShareLink> Hasher = new();

    public ShareService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<IReadOnlyCollection<ShareLinkItemDto>> GetListAsync(TenantContext tenantContext)
    {
        var shares = await _orm.Select<ShareLink>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
        var categoryIds = shares.Select(item => item.CategoryId).Distinct().ToList();
        var categories = categoryIds.Count == 0
            ? []
            : await _orm.Select<Folder>()
                .DisableGlobalFilter("TenantFilter")
                .Where(item => item.TenantId == tenantContext.TenantId && categoryIds.Contains(item.Id))
                .ToListAsync();
        var categoryMap = categories.ToDictionary(item => item.Id, item => item.Name);

        return shares.Select(item => new ShareLinkItemDto
        {
            Id = item.Id,
            ShareCode = item.AccessToken ?? string.Empty,
            CategoryId = item.CategoryId,
            CategoryName = categoryMap.GetValueOrDefault(item.CategoryId, string.Empty),
            HasPassword = !string.IsNullOrEmpty(item.PasswordHash),
            Note = item.Note,
            ExpireAt = Utc(item.ExpireAt),
            CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc)
        }).ToList();
    }

    public async Task<ShareLinkItemDto> CreateAsync(SaveShareLinkRequest request, TenantContext tenantContext)
    {
        var category = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == request.CategoryId && item.TenantId == tenantContext.TenantId)
            .ToOneAsync();
        if (category is null)
        {
            throw new InvalidOperationException("分享分类不存在。");
        }

        var password = request.Password?.Trim();
        if (!string.IsNullOrEmpty(password) && password.Length < 6)
            throw new InvalidOperationException("访问密码至少 6 位。");
        var expires = request.NeverExpires ? (DateTime?)null : request.ExpireAt?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(7);
        if (expires.HasValue && expires.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("过期时间必须晚于当前时间。");

        var share = new ShareLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            CategoryId = request.CategoryId,
            ShareCode = Guid.NewGuid().ToString("N")[..8],
            AccessToken = NewToken(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ExpireAt = expires,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(password)) share.PasswordHash = Hasher.HashPassword(share, password);
        await _orm.Insert(share).ExecuteAffrowsAsync();

        return new ShareLinkItemDto
        {
            Id = share.Id,
            ShareCode = share.AccessToken!,
            CategoryId = share.CategoryId,
            CategoryName = category.Name,
            HasPassword = !string.IsNullOrEmpty(share.PasswordHash),
            Note = share.Note,
            ExpireAt = share.ExpireAt,
            CreatedAt = share.CreatedAt
        };
    }

    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    // Existing records keep their id/category/expiry, but old non-functional short URLs are retired.
    public async Task UpgradeLegacyAsync()
    {
        var legacy = await _orm.Select<ShareLink>().DisableGlobalFilter("TenantFilter")
            .Where(x => x.AccessToken == null || x.Password != null).ToListAsync();
        foreach (var share in legacy)
        {
            var token = share.AccessToken ?? NewToken();
            var hash = share.PasswordHash;
            if (!string.IsNullOrEmpty(share.Password) && string.IsNullOrEmpty(hash))
                hash = Hasher.HashPassword(share, share.Password);
            await _orm.Update<ShareLink>().DisableGlobalFilter("TenantFilter")
                .Where(x => x.Id == share.Id).Set(x => x.AccessToken, token)
                .Set(x => x.PasswordHash, hash).Set(x => x.Password, (string?)null).ExecuteAffrowsAsync();
        }
    }

    private async Task<(ShareLink Share, Folder Category)> ResolveAsync(string code)
    {
        if (code.Length != 64 || code.Any(c => !char.IsAsciiHexDigit(c)))
            throw new KeyNotFoundException("分享不存在、已过期或已撤销。");
        var share = await _orm.Select<ShareLink>().DisableGlobalFilter("TenantFilter")
            .Where(x => x.AccessToken == code).ToOneAsync();
        if (share is null || share.ExpireAt.HasValue && share.ExpireAt.Value <= DateTime.UtcNow)
            throw new KeyNotFoundException("分享不存在、已过期或已撤销。");
        var ownerActive = await _orm.Select<Tenant>().Where(x => x.Id == share.TenantId && x.IsActive).AnyAsync();
        if (!ownerActive) throw new KeyNotFoundException("分享不存在、已过期或已撤销。");
        var category = await _orm.Select<Folder>().DisableGlobalFilter("TenantFilter")
            .Where(x => x.Id == share.CategoryId && x.TenantId == share.TenantId).ToOneAsync();
        if (category is null) throw new KeyNotFoundException("分享不存在、已过期或已撤销。");
        return (share, category);
    }

    public async Task<PublicShareInfo> GetPublicInfoAsync(string code)
    {
        var (share, category) = await ResolveAsync(code);
        return new(category.Name, !string.IsNullOrEmpty(share.PasswordHash), Utc(share.ExpireAt));
    }

    public async Task<PublicShareContent> ReadAsync(string code, string? password)
    {
        var (share, category) = await ResolveAsync(code);
        if (!string.IsNullOrEmpty(share.PasswordHash))
        {
            PasswordVerificationResult result;
            try { result = Hasher.VerifyHashedPassword(share, share.PasswordHash, password?.Trim() ?? ""); }
            catch (FormatException) { result = PasswordVerificationResult.Failed; }
            if (result == PasswordVerificationResult.Failed) throw new UnauthorizedAccessException("访问密码不正确。");
        }
        var folders = await _orm.Select<Folder>().DisableGlobalFilter("TenantFilter")
            .Where(x => x.TenantId == share.TenantId).OrderByDescending(x => x.SortOrder).ToListAsync();
        var selected = new HashSet<Guid> { category.Id };
        // Include descendants, never siblings or folders belonging to another tenant.
        bool changed;
        do {
            changed = false;
            foreach (var folder in folders)
                if (folder.ParentId.HasValue && selected.Contains(folder.ParentId.Value)) changed |= selected.Add(folder.Id);
        } while (changed);
        var ids = selected.ToList();
        var bookmarks = await _orm.Select<Bookmark>().DisableGlobalFilter("TenantFilter")
            .Where(x => x.TenantId == share.TenantId && x.FolderId.HasValue && ids.Contains(x.FolderId.Value))
            .OrderByDescending(x => x.IsPinned).OrderByDescending(x => x.SortOrder).ToListAsync();
        var content = folders.Where(x => selected.Contains(x.Id)).Select(folder => new SharedCategory(folder.Name,
            bookmarks.Where(x => x.FolderId == folder.Id && IsSafeUrl(x.Url))
                .Select(x => new SharedBookmark(x.Title, x.Url, x.Description)).ToList())).ToList();
        // Recheck after assembling the response so an expiry/revocation during the read is respected.
        await ResolveAsync(code);
        return new(category.Name, Utc(share.ExpireAt), content);
    }

    private static bool IsSafeUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http") && string.IsNullOrEmpty(uri.UserInfo);

    public async Task DeleteAsync(Guid id, TenantContext tenantContext)
    {
        var deleted = await _orm.Delete<ShareLink>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == id && item.TenantId == tenantContext.TenantId)
            .ExecuteAffrowsAsync();
        if (deleted == 0)
        {
            throw new KeyNotFoundException("分享记录不存在。");
        }
    }
}
