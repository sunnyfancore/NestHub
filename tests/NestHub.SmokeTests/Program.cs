using System.Net;
using Microsoft.Extensions.Configuration;
using FreeSql;
using Microsoft.Data.Sqlite;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Services;
using NestHub.Api.Infrastructure.MultiTenancy;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

var icons = SiteIconService.ParseIcons("""
    <base href='/app/'>
    <link HREF='images/apple.png' REL='apple-touch-icon'>
    <link href="/api/branding/icon?v=1&amp;size=32" rel='shortcut icon'>
    <link rel='icon' href='data:image/svg+xml,ignored'>
    <link rel='icon' href='https://user:password@example.com/private.png'>
    """, new Uri("https://example.com/dashboard"));
Check(icons.SequenceEqual(new[] { "https://example.com/api/branding/icon?v=1&size=32", "https://example.com/app/images/apple.png" }), "Icon declaration parsing failed");
foreach (var ip in new[] { "127.0.0.1", "10.0.0.2", "172.31.1.1", "192.168.1.1", "169.254.169.254", "100.64.0.1", "::1", "fc00::1", "fe80::1", "::ffff:192.168.1.1" })
    Check(!SiteIconService.IsPublicAddress(IPAddress.Parse(ip)), "Private address allowed: " + ip);
foreach (var ip in new[] { "8.8.8.8", "1.1.1.1", "2606:4700:4700::1111" })
    Check(SiteIconService.IsPublicAddress(IPAddress.Parse(ip)), "Public address rejected: " + ip);
using (var discovery = new SiteIconService())
{
    Check((await discovery.DiscoverAsync("http://127.0.0.1/favicon", CancellationToken.None)).Count == 0, "Loopback discovery allowed");
    Check(await discovery.LoadImageAsync("http://127.0.0.1/favicon.ico", CancellationToken.None) is null, "Loopback image proxy allowed");
    Check(await discovery.LoadImageAsync("https://user:password@example.com/favicon.ico", CancellationToken.None) is null, "Image proxy accepted embedded credentials");
}
Console.WriteLine("ICON_DECLARATIONS_AND_NETWORK_GUARDS_OK");

var directory = Path.Combine(Path.GetTempPath(), "nesthub-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var sourceString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "source.db"), Pooling = false }.ConnectionString;
    var targetString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "target.db"), Pooling = false }.ConnectionString;
    using var source = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, sourceString).Build();
    using var target = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, targetString).Build();
    DatabaseMigrationService.CreateSchema(source);
    DatabaseMigrationService.CreateSchema(target);
    var id = Guid.NewGuid(); var folder = Guid.NewGuid(); var bookmark = Guid.NewGuid();
    var now = new DateTime(2026, 10, 10, 10, 20, 30, 123).AddTicks(4560);
    await source.Insert(new Tenant { Id = id, Name = "迁移租户", Email = "test@example.com", PasswordHash = "original-password-hash", IsSuperAdmin = true, CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    await source.Insert(new SiteSetting { Id = Guid.NewGuid(), TenantId = id, Title = "原导航", ThemeName = "liquid", CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    var siteService = new SiteSettingService(source);
    await siteService.UpdateThemeAsync("blackgold", new TenantContext(id, id, "迁移租户", "test@example.com", "迁移租户"));
    var themeSetting = await source.Select<SiteSetting>().Where(item => item.TenantId == id).ToOneAsync();
    Check(themeSetting.Title == "原导航" && themeSetting.ThemeName == "blackgold" && themeSetting.MobileThemeName == "blackgold", "Theme-only update lost settings or mobile theme");
    await source.Insert(new Folder { Id = folder, TenantId = id, Name = "中文分类", CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    await source.Insert(new Bookmark { Id = bookmark, TenantId = id, FolderId = folder, Title = "自建站", Url = "https://example.com", IconUrl = "https://example.com/api/branding/icon", CreatedAt = now, UpdatedAt = now, IsPinned = true, ClickCount = 17 }).ExecuteAffrowsAsync();
    await source.Insert(new ShareLink { Id = Guid.NewGuid(), TenantId = id, CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    await source.Insert(new TransitionSetting { Id = Guid.NewGuid(), TenantId = id, AdScript1 = "测试脚本", CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    await source.Insert(new SmtpSetting { Id = Guid.NewGuid(), Host = "mail.example.com", Username = "test", Password = "smtp-secret", CreatedAt = now, UpdatedAt = now }).ExecuteAffrowsAsync();
    await source.Insert(new PasswordResetToken { Id = Guid.NewGuid(), UserId = id, Token = "reset-token", CreatedAt = now, ExpiresAt = now.AddHours(1) }).ExecuteAffrowsAsync();
    await using var input = new SqliteConnection(sourceString); await input.OpenAsync();
    await using var output = new SqliteConnection(targetString); await output.OpenAsync();
    await using var read = await input.BeginTransactionAsync();
    await using var write = await output.BeginTransactionAsync();
    await DatabaseMigrationService.CopyAllAsync(source, target, read, write);
    await write.CommitAsync(); await read.CommitAsync();
    Check((await target.Select<Tenant>().FirstAsync()).PasswordHash == "original-password-hash", "Password hash changed");
    Check((await target.Select<Bookmark>().FirstAsync()).ClickCount == 17, "Bookmark state changed");
    Console.WriteLine("SQLITE_EIGHT_TABLES_FULL_FIELD_COPY_OK");
}
finally { Directory.Delete(directory, recursive: true); }

var updateConfiguration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Updates:Enabled"] = "true", ["Updates:ServiceUrl"] = "http://updater:8080",
    ["Updates:Token"] = "test-updater-token", ["NESTHUB_BUILD_REVISION"] = new string('a', 40)
}).Build();
using var updateClient = new HttpClient(new UpdateHandler());
using var updates = new SystemUpdateService(updateConfiguration, updateClient);
Check((await updates.CheckAsync(CancellationToken.None)).HasUpdate, "New image was not detected");
var accessor = new NestHub.Api.Infrastructure.MultiTenancy.TenantContextAccessor();
var controller = new NestHub.Api.Controllers.SystemUpdatesController(updates, accessor);
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Anonymous update allowed");
accessor.Set(new(Guid.NewGuid(), Guid.NewGuid(), "user", "user@example.com", "tenant"));
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Tenant administrator update allowed");
await updates.ApplyAsync(CancellationToken.None);
try { await updates.ApplyAsync(CancellationToken.None); throw new Exception("Duplicate update allowed"); }
catch (InvalidOperationException) { }
Console.WriteLine("UPDATE_AUTHORIZATION_TOKEN_AND_DUPLICATE_GUARDS_OK");

sealed class UpdateHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.Host == "updater")
        {
            if (request.Headers.Authorization?.ToString() != "Bearer test-updater-token" || request.RequestUri.AbsolutePath != "/v1/update")
                throw new Exception("Wrong update credentials or target");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"workflow_runs\":[{\"head_sha\":\"" + new string('b', 40) + "\",\"updated_at\":\"2026-10-10T00:00:00Z\"}]}")
        });
    }
}
