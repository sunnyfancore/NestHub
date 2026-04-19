using FreeSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.Configuration;

namespace NestHub.Api.Infrastructure.Persistence;

public sealed class DataSeeder
{
    private readonly IFreeSql _orm;
    private readonly IPasswordHasher<Tenant> _passwordHasher;
    private readonly SuperAdminOptions _superAdminOptions;

    public DataSeeder(
        IFreeSql orm,
        IPasswordHasher<Tenant> passwordHasher,
        IOptions<SuperAdminOptions> superAdminOptions)
    {
        _orm = orm;
        _passwordHasher = passwordHasher;
        _superAdminOptions = superAdminOptions.Value;
    }

    public async Task SeedAsync()
    {
        var now = DateTime.UtcNow;

        // Ensure public tenant exists
        if (!await _orm.Select<Tenant>().Where(t => t.Id == Tenant.PublicTenantId).AnyAsync())
        {
            await _orm.Insert(new Tenant
            {
                Id = Tenant.PublicTenantId,
                Name = "公共导航",
                CreatedAt = now,
                UpdatedAt = now
            }).ExecuteAffrowsAsync();

            await _orm.Insert(new SiteSetting
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant.PublicTenantId,
                Title = "NestHub 公共导航",
                Subtitle = "为团队与设备准备的一站式导航门户",
                LogoText = "N",
                SearchPlaceholder = "搜索链接、标题或分类名称...",
                FooterText = "由 NestHub 管理维护",
                ThemeName = "default2",
                CreatedAt = now,
                UpdatedAt = now
            }).ExecuteAffrowsAsync();

            await SeedPublicDataAsync(now);
        }

        // Ensure super admin tenant exists
        if (await _orm.Select<Tenant>().Where(t => t.IsSuperAdmin).AnyAsync())
        {
            return;
        }

        var admin = new Tenant
        {
            Id = Guid.NewGuid(),
            Email = _superAdminOptions.Email.Trim().ToLowerInvariant(),
            DisplayName = _superAdminOptions.DisplayName.Trim(),
            Name = _superAdminOptions.DisplayName.Trim(),
            IsActive = true,
            IsSuperAdmin = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, _superAdminOptions.Password);

        await _orm.Insert(admin).ExecuteAffrowsAsync();
    }

    private async Task SeedPublicDataAsync(DateTime now)
    {
        var tid = Tenant.PublicTenantId;
        var categories = new List<Folder>();
        var links = new List<Bookmark>();

        var catFav = NewFolder(tid, null, "常用推荐", "最常访问的网站和服务", "star", 900, now);
        categories.Add(catFav);
        links.AddRange(new[] {
            NewBookmark(tid, catFav.Id, "百度", "https://www.baidu.com", 900, now),
            NewBookmark(tid, catFav.Id, "哔哩哔哩", "https://www.bilibili.com", 890, now),
            NewBookmark(tid, catFav.Id, "知乎", "https://www.zhihu.com", 880, now),
            NewBookmark(tid, catFav.Id, "微博", "https://weibo.com", 870, now),
            NewBookmark(tid, catFav.Id, "淘宝", "https://www.taobao.com", 860, now),
            NewBookmark(tid, catFav.Id, "京东", "https://www.jd.com", 850, now),
            NewBookmark(tid, catFav.Id, "GitHub", "https://github.com", 840, now),
            NewBookmark(tid, catFav.Id, "高德地图", "https://www.amap.com", 830, now),
        });

        var catDev = NewFolder(tid, null, "开发工具", "开发者常用工具和资源", "code", 800, now);
        categories.Add(catDev);

        var catCodeHost = NewFolder(tid, catDev.Id, "代码托管", "代码托管和协作平台", "github-alt", 900, now);
        categories.Add(catCodeHost);
        links.AddRange(new[] {
            NewBookmark(tid, catCodeHost.Id, "Gitee", "https://gitee.com", 900, now),
            NewBookmark(tid, catCodeHost.Id, "GitHub", "https://github.com", 880, now),
            NewBookmark(tid, catCodeHost.Id, "GitLab", "https://gitlab.com", 870, now),
        });

        var catAI = NewFolder(tid, catDev.Id, "AI 助手", "AI 编程和对话助手", "robot", 880, now);
        categories.Add(catAI);
        links.AddRange(new[] {
            NewBookmark(tid, catAI.Id, "DeepSeek", "https://chat.deepseek.com", 900, now),
            NewBookmark(tid, catAI.Id, "通义千问", "https://tongyi.aliyun.com", 890, now),
            NewBookmark(tid, catAI.Id, "Kimi", "https://kimi.moonshot.cn", 860, now),
            NewBookmark(tid, catAI.Id, "智谱清言", "https://chatglm.cn", 850, now),
        });

        var catDocs = NewFolder(tid, catDev.Id, "文档参考", "编程文档和技术问答", "book", 870, now);
        categories.Add(catDocs);
        links.AddRange(new[] {
            NewBookmark(tid, catDocs.Id, "掘金", "https://juejin.cn", 890, now),
            NewBookmark(tid, catDocs.Id, "博客园", "https://www.cnblogs.com", 880, now),
            NewBookmark(tid, catDocs.Id, "MDN Web Docs", "https://developer.mozilla.org", 850, now),
        });

        var catProductivity = NewFolder(tid, null, "效率工具", "办公和团队协作工具", "check-square", 700, now);
        categories.Add(catProductivity);
        links.AddRange(new[] {
            NewBookmark(tid, catProductivity.Id, "飞书", "https://www.feishu.cn", 900, now),
            NewBookmark(tid, catProductivity.Id, "钉钉", "https://www.dingtalk.com", 890, now),
            NewBookmark(tid, catProductivity.Id, "语雀", "https://www.yuque.com", 880, now),
            NewBookmark(tid, catProductivity.Id, "Notion", "https://www.notion.so", 840, now),
        });

        var catCloud = NewFolder(tid, null, "云服务", "云计算和部署平台", "cloud", 650, now);
        categories.Add(catCloud);
        links.AddRange(new[] {
            NewBookmark(tid, catCloud.Id, "阿里云", "https://www.aliyun.com", 900, now),
            NewBookmark(tid, catCloud.Id, "腾讯云", "https://cloud.tencent.com", 890, now),
            NewBookmark(tid, catCloud.Id, "Vercel", "https://vercel.com", 840, now),
        });

        var catLearning = NewFolder(tid, null, "学习教育", "在线课程和技术学习平台", "graduation-cap", 600, now);
        categories.Add(catLearning);
        links.AddRange(new[] {
            NewBookmark(tid, catLearning.Id, "极客时间", "https://time.geekbang.org", 900, now),
            NewBookmark(tid, catLearning.Id, "LeetCode 力扣", "https://leetcode.cn", 880, now),
            NewBookmark(tid, catLearning.Id, "菜鸟教程", "https://www.runoob.com", 870, now),
        });

        await _orm.Insert<Folder>().AppendData(categories).ExecuteAffrowsAsync();
        await _orm.Insert<Bookmark>().AppendData(links).ExecuteAffrowsAsync();
    }

    private static Folder NewFolder(Guid tenantId, Guid? parentId, string name, string desc, string icon, int sortOrder, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ParentId = parentId,
        Name = name,
        Description = desc,
        Icon = icon,
        SortOrder = sortOrder,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static Bookmark NewBookmark(Guid tenantId, Guid folderId, string title, string url, int sortOrder, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        FolderId = folderId,
        Title = title,
        Url = url,
        SortOrder = sortOrder,
        CreatedAt = now,
        UpdatedAt = now
    };
}
