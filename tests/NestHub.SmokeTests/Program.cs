using System.Net;
using System.Text.Json.Nodes;
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
    Check(await discovery.LoadImageAsync("http://127.0.0.1/favicon.ico", CancellationToken.None) is null, "Loopback image proxy allowed");
    Check(await discovery.LoadImageAsync("https://user:password@example.com/favicon.ico", CancellationToken.None) is null, "Image proxy accepted embedded credentials");
    Check((await discovery.DiscoverAsync("http://127.0.0.1/favicon", CancellationToken.None)).Count == 0, "Loopback discovery allowed");
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
    var portalService = new PortalService(target);
    await Task.WhenAll(Enumerable.Range(0,20).Select(_ => portalService.RecordClickAsync(bookmark, null)));
    var clicked = await target.Select<Bookmark>().Where(item => item.Id == bookmark).FirstAsync();
    Check(clicked.ClickCount == 37 && clicked.Title == "自建站", "Concurrent clicks lost counts or changed bookmark content");
    try { await portalService.RecordClickAsync(Guid.NewGuid(), null); throw new Exception("Missing link accepted"); } catch (KeyNotFoundException) { }
    var tenantSummary = await new SuperAdminService(target, new Microsoft.AspNetCore.Identity.PasswordHasher<Tenant>()).GetTenantsAsync();
    Check(tenantSummary.Single(item => item.Id == id).LinkCount == 1, "Database grouped tenant counts incorrect");
    Console.WriteLine("ATOMIC_CONCURRENT_CLICKS_AND_DATABASE_GROUPED_COUNTS_OK");
}
finally { Directory.Delete(directory, recursive: true); }

using var disabledUpdates = new SystemUpdateService(new ConfigurationBuilder().AddInMemoryCollection().Build());
Check(!disabledUpdates.Enabled, "Disabled updates allowed");
using var legacyUpdates = new SystemUpdateService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Updates:Enabled"] = "true" }).Build());
Check(!legacyUpdates.Enabled && legacyUpdates.DisabledReason!.Contains("启动器"), "Legacy image accepted updates");
var accessor = new TenantContextAccessor();
var controller = new NestHub.Api.Controllers.SystemUpdatesController(disabledUpdates, accessor);
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Anonymous update allowed");
accessor.Set(new(Guid.NewGuid(), Guid.NewGuid(), "user", "user@example.com", "tenant"));
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Tenant update allowed");
foreach (var url in new[] { "http://github.com/sunnyfancore/NestHub/releases/download/a/a.zip", "https://evil.example/a.zip", "https://github.com/other/repo/releases/download/a/a.zip" })
{
    try { SystemUpdateService.ValidateUrl(url); throw new Exception("Untrusted URL allowed"); } catch (InvalidDataException) { }
}
SystemUpdateService.ValidateUrl("https://github.com/sunnyfancore/NestHub/releases/download/app-test/app.zip");
var updateDirectory = Path.Combine(Path.GetTempPath(), "nesthub-package-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(updateDirectory);
try
{
    foreach (var entry in new[] { "../escape", "AppData/nesthub.db", "C:/escape", "/escape" })
    {
        var zipPath = Path.Combine(updateDirectory, "test.zip");
        using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create)) zip.CreateEntry(entry);
        try { SystemUpdateService.ExtractPackage(zipPath, Path.Combine(updateDirectory, "output")); throw new Exception("Unsafe archive allowed"); } catch (InvalidDataException) { }
        File.Delete(zipPath);
    }
    var good = Path.Combine(updateDirectory, "good.zip");
    using (var zip = System.IO.Compression.ZipFile.Open(good, System.IO.Compression.ZipArchiveMode.Create))
        foreach (var entry in new[] { "NestHub.Api.dll", "NestHub.Api.runtimeconfig.json", "wwwroot/index.html" }) zip.CreateEntry(entry);
    SystemUpdateService.ExtractPackage(good, Path.Combine(updateDirectory, "valid"));
    Check(File.Exists(Path.Combine(updateDirectory, "valid", "wwwroot/index.html")), "Valid package not extracted");
}
finally { Directory.Delete(updateDirectory, true); }
Console.WriteLine("APPLICATION_UPDATE_AUTHORIZATION_AND_ARCHIVE_GUARDS_OK");

var manifest = JsonNode.Parse("""{"revision":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","runtime":"net10.0","packages":{"linux-x64":{"url":"https://github.com/sunnyfancore/NestHub/releases/download/app-test/app.zip","size":10,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}}""")!.AsObject();
SystemUpdateService.ValidateManifest(manifest, "linux-x64");
foreach (var mode in new[] { "runtime", "hash", "size", "revision" })
{
    var invalid = (JsonObject)manifest.DeepClone();
    if (mode == "runtime") invalid["runtime"] = "net11.0";
    if (mode == "revision") invalid["revision"] = "../escape";
    if (mode == "hash") invalid["packages"]!["linux-x64"]!["sha256"] = "bad";
    if (mode == "size") invalid["packages"]!["linux-x64"]!["size"] = 99999999999L;
    try { SystemUpdateService.ValidateManifest(invalid, "linux-x64"); throw new Exception("Invalid manifest allowed: " + mode); } catch (InvalidDataException) { }
}
Console.WriteLine("APPLICATION_MANIFEST_RUNTIME_AND_SIZE_CHECKS_OK");

if (OperatingSystem.IsLinux())
{
    foreach (var corrupt in new[] { true, false })
    {
        var root = Path.Combine(Path.GetTempPath(), "nesthub-install-" + Guid.NewGuid().ToString("N"));
        var lifetime = new UpdateTestLifetime();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Updates:Enabled"] = "true", ["Updates:DataPath"] = root, ["NESTHUB_APP_LAUNCHER"] = "1", ["NESTHUB_BUILD_REVISION"] = new string('a',40) }).Build();
        using var service = new SystemUpdateService(config, lifetime, new HttpClient(new ApplicationPackageHandler(corrupt)));
        try
        {
            Check((await service.CheckAsync(CancellationToken.None)).HasUpdate, "Application release not detected");
            await service.ApplyAsync(CancellationToken.None);
            try { await service.ApplyAsync(CancellationToken.None); throw new Exception("Duplicate application update accepted"); } catch (InvalidOperationException) { }
            for (var i = 0; i < 100 && service.Progress().UpdateState is not ("failed" or "checking"); i++) await Task.Delay(100);
            if (corrupt)
            {
                Check(service.Progress().UpdateState == "failed", "Corrupt package not rejected");
                Check(!File.Exists(Path.Combine(root,"updates","pending.json")) && !lifetime.Stopped, "Corrupt package activated");
            }
            else
            {
                for (var i = 0; i < 30 && !lifetime.Stopped; i++) await Task.Delay(100);
                Check(lifetime.Stopped && File.Exists(Path.Combine(root,"updates","pending.json")), "Valid package not scheduled for restart");
                Check(Directory.Exists(Path.Combine(root,"updates","releases",new string('b',40),"AppData")), "Persistent data not linked");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    Console.WriteLine("APPLICATION_DOWNLOAD_CHECKSUM_ACTIVATION_AND_DUPLICATE_GUARDS_OK");
}

sealed class UpdateTestLifetime : Microsoft.Extensions.Hosting.IHostApplicationLifetime
{
    public volatile bool Stopped;
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() => Stopped = true;
}
sealed class ApplicationPackageHandler(bool corrupt) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
            foreach (var name in new[] { "NestHub.Api.dll", "NestHub.Api.runtimeconfig.json", "wwwroot/index.html" })
            { var entry = zip.CreateEntry(name); entry.LastWriteTime = new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero); using var writer = new StreamWriter(entry.Open()); writer.Write(name.EndsWith("runtimeconfig.json") ? "{\"runtimeOptions\":{\"tfm\":\"net10.0\"}}" : "test"); }
        var bytes = stream.ToArray();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        var package = new JsonObject { ["url"] = "https://github.com/sunnyfancore/NestHub/releases/download/app-test/app.zip", ["size"] = bytes.Length, ["sha256"] = corrupt ? new string('0',64) : hash };
        var manifest = new JsonObject { ["revision"] = new string('b',40), ["runtime"] = "net10.0", ["packages"] = new JsonObject { ["linux-x64"] = package, ["linux-arm64"] = package.DeepClone() } };
        HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("app.zip") ? new ByteArrayContent(bytes) :
            new StringContent(request.RequestUri.AbsolutePath.Contains("/compare/") ? "{\"status\":\"ahead\"}" : request.RequestUri.Host == "api.github.com" ? """[{"draft":false,"tag_name":"app-test","assets":[{"name":"manifest.json","browser_download_url":"https://github.com/sunnyfancore/NestHub/releases/download/app-test/manifest.json"}]}]""" : manifest.ToJsonString());
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
