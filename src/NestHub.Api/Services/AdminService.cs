using System.Net;
using System.Text.RegularExpressions;
using FreeSql;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;

namespace NestHub.Api.Services;

public sealed class AdminService
{
    private readonly IFreeSql _orm;

    public AdminService(IFreeSql orm)
    {
        _orm = orm;
    }

    public async Task<IReadOnlyCollection<AdminCategoryItemDto>> GetCategoriesAsync(TenantContext tenantContext)
    {
        var categories = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .OrderByDescending(item => item.SortOrder)
            .OrderBy(item => item.Name)
            .ToListAsync();

        var categoryMap = categories.ToDictionary(item => item.Id);
        var categoryIds = categories.Select(item => item.Id).ToList();
        var linkCounts = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId && item.FolderId != null && categoryIds.Contains(item.FolderId.Value))
            .GroupBy(item => item.FolderId!.Value)
            .ToListAsync(item => new { FolderId = item.Key, Count = item.Count() });
        var linkCountMap = linkCounts.ToDictionary(item => item.FolderId, item => item.Count);

        // Build child folder lookup for parent link count aggregation
        var childFolderMap = categories
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        int GetLinkCount(Guid folderId)
        {
            var count = linkCountMap.GetValueOrDefault(folderId);
            if (childFolderMap.TryGetValue(folderId, out var childIds))
            {
                foreach (var childId in childIds)
                {
                    count += linkCountMap.GetValueOrDefault(childId);
                }
            }
            return count;
        }

        return categories.Select(item => new AdminCategoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            ParentId = item.ParentId,
            ParentName = item.ParentId.HasValue ? categoryMap.GetValueOrDefault(item.ParentId.Value)?.Name : null,
            Description = item.Description,
            Icon = item.Icon,
            SortOrder = item.SortOrder,
            LinkCount = GetLinkCount(item.Id),
            UpdatedAt = item.UpdatedAt
        }).ToList();
    }

    public async Task<IReadOnlyCollection<AdminLinkItemDto>> GetLinksAsync(TenantContext tenantContext, string? keyword = null, Guid? categoryId = null)
    {
        var categories = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .ToListAsync();
        var categoryMap = categories.ToDictionary(item => item.Id, item => item.Name);
        var query = _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId);

        if (categoryId.HasValue)
        {
            query = query.Where(item => item.FolderId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            query = query.Where(item =>
                item.Title.Contains(keyword) ||
                item.Url.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
        }

        var links = await query
            .OrderByDescending(item => item.SortOrder)
            .OrderBy(item => item.Title)
            .ToListAsync();

        return links.Select(item => new AdminLinkItemDto
        {
            Id = item.Id,
            CategoryId = item.FolderId ?? Guid.Empty,
            CategoryName = item.FolderId.HasValue ? categoryMap.GetValueOrDefault(item.FolderId.Value, string.Empty) : string.Empty,
            Title = item.Title,
            Url = item.Url,
            StandbyUrl = item.StandbyUrl,
            Description = item.Description,
            Tags = item.Tags,
            IconUrl = item.IconUrl,
            IsPinned = item.IsPinned,
            SortOrder = item.SortOrder,
            ClickCount = item.ClickCount,
            UpdatedAt = item.UpdatedAt
        }).ToList();
    }

    public IReadOnlyCollection<AdminThemeOptionDto> GetThemes()
    {
        return
        [
            new AdminThemeOptionDto
            {
                Name = "default2",
                Title = "default2",
                Version = "1.3.5",
                Description = "当前最完整的默认主题，适合桌面与移动端导航站。",
                ScreenshotUrl = "https://v.png.pub/imgs/2024/11/27/c01894e5d9e0d850.png"
            },
            new AdminThemeOptionDto
            {
                Name = "ocean",
                Title = "Ocean",
                Version = "1.0.0",
                Description = "海洋蔚蓝主题，深蓝侧边栏搭配清爽白色内容区，专业大气。",
                ScreenshotUrl = ""
            },
            new AdminThemeOptionDto
            {
                Name = "nord",
                Title = "Nord",
                Version = "1.0.0",
                Description = "北欧极简主题，冰蓝色调搭配斯堪的纳维亚配色，沉静舒适。",
                ScreenshotUrl = ""
            },
            new AdminThemeOptionDto
            {
                Name = "glass",
                Title = "Glass",
                Version = "1.0.0",
                Description = "玻璃拟态主题，磨砂透明效果搭配渐变背景，未来感十足。",
                ScreenshotUrl = ""
            },
            new AdminThemeOptionDto
            {
                Name = "neon",
                Title = "Neon",
                Version = "1.0.0",
                Description = "赛博朋克主题，霓虹灯光效果搭配暗色基底，科技潮流感。",
                ScreenshotUrl = ""
            },
            new AdminThemeOptionDto
            {
                Name = "paper",
                Title = "Paper",
                Version = "1.0.0",
                Description = "暖色纸张主题，温润书卷气息搭配棕金色调，文艺雅致。",
                ScreenshotUrl = ""
            }
        ];
    }

    public async Task<BackupExportDto> ExportAsync(TenantContext tenantContext)
    {
        var site = await _orm.Select<SiteSetting>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId).ToOneAsync();
        var categories = await GetCategoriesAsync(tenantContext);
        var links = await GetLinksAsync(tenantContext);

        return new BackupExportDto
        {
            Site = new
            {
                site?.Title,
                site?.Subtitle,
                site?.Description,
                site?.LogoText,
                site?.SearchPlaceholder,
                site?.FooterText,
                site?.ThemeName,
                site?.MobileThemeName
            },
            Categories = categories,
            Links = links
        };
    }

    public async Task<string> ExportBookmarkHtmlAsync(TenantContext tenantContext, BookmarkHtmlGenerator generator)
    {
        var tenant = await _orm.Select<Tenant>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.Id == tenantContext.TenantId).ToOneAsync();
        var folders = await _orm.Select<Folder>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .ToListAsync();
        var links = await _orm.Select<Bookmark>()
            .DisableGlobalFilter("TenantFilter")
            .Where(item => item.TenantId == tenantContext.TenantId)
            .ToListAsync();

        return generator.GenerateHtml(tenant?.Name ?? "Bookmarks", folders, links);
    }

    public async Task<BookmarkImportResultDto> ImportBookmarksAsync(Guid? categoryId, Stream fileStream, TenantContext tenantContext)
    {
        if (categoryId.HasValue)
        {
            var categoryExists = await _orm.Select<Folder>()
                .DisableGlobalFilter("TenantFilter")
                .Where(item => item.Id == categoryId.Value && item.TenantId == tenantContext.TenantId)
                .AnyAsync();

            if (!categoryExists)
            {
                throw new InvalidOperationException("导入目标分类不存在。");
            }
        }

        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();

        return await ImportWithFoldersAsync(content, tenantContext, categoryId);
    }

    private async Task<BookmarkImportResultDto> ImportWithFoldersAsync(string content, TenantContext tenantContext, Guid? rootParentId = null)
    {
        var now = DateTime.UtcNow;
        var total = 0;
        var imported = 0;
        var failed = 0;

        var foldersToInsert = new List<Folder>();
        var linksToInsert = new List<Bookmark>();
        var seenUrls = new HashSet<string>();

        // Stack holds (Name, FolderId, ParentId) — FolderId is assigned immediately when <H3> is seen
        var folderStack = new List<(string Name, Guid FolderId, Guid? ParentId)>();
        var sortOrder = 0;

        var lines = content.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            var folderMatch = Regex.Match(line, @"<DT><H3[^>]*>(?<name>.*?)</H3>", RegexOptions.IgnoreCase);
            if (folderMatch.Success)
            {
                var name = WebUtility.HtmlDecode(folderMatch.Groups["name"].Value.Trim());
                if (!string.IsNullOrEmpty(name))
                {
                    var folderId = Guid.NewGuid();
                    var parentId = folderStack.Count > 0 ? (Guid?)folderStack[^1].FolderId : rootParentId;
                    folderStack.Add((name, folderId, parentId));
                    sortOrder += 10;

                    var iconMatch = Regex.Match(line, @"ICON=""(?<icon>[^""]+)""", RegexOptions.IgnoreCase);
                    var icon = iconMatch.Success ? WebUtility.HtmlDecode(iconMatch.Groups["icon"].Value.Trim()) : null;

                    foldersToInsert.Add(new Folder
                    {
                        Id = folderId,
                        TenantId = tenantContext.TenantId,
                        ParentId = parentId,
                        Name = name,
                        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon,
                        SortOrder = sortOrder,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                continue;
            }

            if (line.StartsWith("</DL>", StringComparison.OrdinalIgnoreCase))
            {
                if (folderStack.Count > 0)
                {
                    folderStack.RemoveAt(folderStack.Count - 1);
                }
                continue;
            }

            var linkMatch = Regex.Match(line, @"<A[^>]*HREF=""(?<url>[^""]+)""[^>]*>(?<title>.*?)</A>", RegexOptions.IgnoreCase);
            if (linkMatch.Success)
            {
                total++;
                var url = WebUtility.HtmlDecode(linkMatch.Groups["url"].Value.Trim());
                var title = WebUtility.HtmlDecode(Regex.Replace(linkMatch.Groups["title"].Value, "<.*?>", string.Empty).Trim());

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title) || seenUrls.Contains(url))
                {
                    failed++;
                    continue;
                }

                Guid? folderId = rootParentId;
                if (folderStack.Count > 0)
                {
                    folderId = folderStack[^1].FolderId;
                }

                var linkIconMatch = Regex.Match(line, @"ICON=""(?<icon>[^""]+)""", RegexOptions.IgnoreCase);
                var linkIcon = linkIconMatch.Success ? WebUtility.HtmlDecode(linkIconMatch.Groups["icon"].Value.Trim()) : null;

                sortOrder += 10;
                linksToInsert.Add(new Bookmark
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    FolderId = folderId,
                    Title = title,
                    Url = url,
                    IconUrl = linkIcon,
                    SortOrder = sortOrder,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                seenUrls.Add(url);
                imported++;
            }
        }

        if (foldersToInsert.Count > 0)
        {
            // Reverse sort orders: HTML lists highest sortOrder first, but import assigns
            // increasing values, which would reverse the display order.
            var maxSort = foldersToInsert.Max(f => f.SortOrder) + 10;
            foreach (var f in foldersToInsert)
            {
                f.SortOrder = maxSort - f.SortOrder;
            }

            await _orm.Insert<Folder>().AppendData(foldersToInsert).ExecuteAffrowsAsync();
        }

        if (linksToInsert.Count > 0)
        {
            // Reverse sort orders: same reason as folders
            var maxLinkSort = linksToInsert.Max(l => l.SortOrder) + 10;
            foreach (var l in linksToInsert)
            {
                l.SortOrder = maxLinkSort - l.SortOrder;
            }

            await _orm.Insert<Bookmark>().AppendData(linksToInsert).ExecuteAffrowsAsync();
        }

        return new BookmarkImportResultDto
        {
            Total = total,
            Imported = imported,
            Failed = failed
        };
    }
}
