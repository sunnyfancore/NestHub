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
}
finally { Directory.Delete(directory, recursive: true); }

var updateConfiguration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Updates:Enabled"] = "true", ["Updates:ContainerName"] = "nesthub",
    ["NESTHUB_BUILD_REVISION"] = new string('a', 40)
}).Build();
using var updateClient = new HttpClient(new UpdateHandler());
using var dockerClient = new DockerEngineClient("unused", new DockerUpdateHandler());
using var updates = new SystemUpdateService(updateConfiguration, updateClient, dockerClient);
Check((await updates.CheckAsync(CancellationToken.None)).HasUpdate, "New image was not detected");
var accessor = new NestHub.Api.Infrastructure.MultiTenancy.TenantContextAccessor();
var controller = new NestHub.Api.Controllers.SystemUpdatesController(updates, accessor);
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Anonymous update allowed");
accessor.Set(new(Guid.NewGuid(), Guid.NewGuid(), "user", "user@example.com", "tenant"));
Check(await controller.Apply(CancellationToken.None) is Microsoft.AspNetCore.Mvc.ForbidResult, "Tenant administrator update allowed");
await updates.ApplyAsync(CancellationToken.None);
try { await updates.ApplyAsync(CancellationToken.None); throw new Exception("Duplicate update allowed"); }
catch (InvalidOperationException) { }
using var disabledUpdates = new SystemUpdateService(new ConfigurationBuilder().AddInMemoryCollection().Build());
Check(!disabledUpdates.Enabled && disabledUpdates.DisabledReason!.Contains("Updates__Enabled"), "Missing update configuration reason absent");
using var missingNameUpdates = new SystemUpdateService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Updates:Enabled"] = "true" }).Build());
Check(!missingNameUpdates.Enabled && missingNameUpdates.DisabledReason!.Contains("容器名称"), "Missing container name reason absent");
Console.WriteLine("UPDATE_AUTHORIZATION_TOKEN_AND_DUPLICATE_GUARDS_OK");

var updateTestDirectory = Path.Combine(Path.GetTempPath(), "nesthub-update-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(updateTestDirectory);
try
{
    foreach (var mode in new[] { "success", "health-failure", "pull-failure", "unchanged" })
    {
        var handler = new DockerUpdateHandler(mode);
        var docker = new DockerEngineClient("unused", handler);
        var resultFile = Path.Combine(updateTestDirectory, mode + ".json");
        var code = await DockerSelfUpdate.RunAsync(docker, "old", resultFile, () => Task.CompletedTask);
        var report = JsonNode.Parse(await File.ReadAllTextAsync(resultFile))!;
        if (mode == "success")
        {
            Check(code == 0 && report["state"]!.GetValue<string>() == "success", "Self update failed");
            var replacement = handler.Replacement!;
            Check(replacement["Env"]!.AsArray().Any(e => e!.GetValue<string>() == "NESTHUB_BUILD_REVISION=" + new string('b', 40)), "New revision metadata lost");
            Check(replacement["Env"]!.AsArray().Any(e => e!.GetValue<string>() == "Jwt__SecretKey=original-test-key"), "Application environment lost");
            Check(replacement["HostConfig"]!["PortBindings"]!["8080/tcp"]![0]!["HostPort"]!.GetValue<string>() == "5186", "Host port lost");
            Check(replacement["HostConfig"]!["Binds"]!.AsArray().Any(e => e!.GetValue<string>() == "original-volume:/app/AppData:rw"), "Named volume lost");
            Check(replacement["NetworkingConfig"]!["EndpointsConfig"]!["nesthub_default"]!["Aliases"]![0]!.GetValue<string>() == "nesthub", "Network alias lost");
            Check(handler.Calls.Contains("DELETE /containers/old"), "Old container not cleaned up");
        }
        else if (mode == "health-failure")
        {
            Check(code == 1 && handler.Calls.Contains("POST /containers/old/start"), "Failed update did not restart old container");
            Check(handler.Calls.Any(c => c.StartsWith("POST /networks/nesthub_default/connect")), "Rollback network not restored");
            Check(handler.Calls.Contains("DELETE /containers/replacement?force=true"), "Failed replacement not removed");
        }
        else
        {
            Check(!handler.Calls.Any(c => c.Contains("/old/stop")), "Pull failure/no new image stopped the app");
            Check(report["state"]!.GetValue<string>() == (mode == "unchanged" ? "unchanged" : "failed"), "Incorrect update outcome");
        }
    }
}
finally { Directory.Delete(updateTestDirectory, true); }
Console.WriteLine("SELF_UPDATE_CONFIG_PRESERVATION_PULL_FAILURE_HEALTH_ROLLBACK_OK");

var structuredInspect = JsonNode.Parse("""
{"Config":{"Image":"test:latest","Env":[]},"HostConfig":{"Mounts":[{"Type":"volume","Source":"existing","Target":"/app/AppData"}]},
"Mounts":[{"Type":"volume","Name":"existing","Destination":"/app/AppData","RW":true}],"NetworkSettings":{"Networks":{}}}
""")!.AsObject();
var structuredReplacement = DockerSelfUpdate.CreateReplacement(structuredInspect, JsonNode.Parse("""{"Config":{"Env":[]}}""")!.AsObject());
Check(structuredReplacement["HostConfig"]!["Binds"]!.AsArray().Count == 0, "Structured volume duplicated into bind mounts");
Check(structuredReplacement["HostConfig"]!["Mounts"]![0]!["Source"]!.GetValue<string>() == "existing", "Structured volume source lost");

sealed class DockerUpdateHandler(string mode = "success") : HttpMessageHandler
{
    public List<string> Calls { get; } = [];
    public JsonNode? Replacement { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        Calls.Add(request.Method + " " + path);
        JsonNode? body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        string result = "";
        if (path is "/containers/old/json" or "/containers/nesthub/json") result = """
        {"Id":"old","Image":"sha256:old","Name":"/nesthub","State":{"Running":true},
        "Config":{"Image":"registry.example/nesthub:latest","Env":["NESTHUB_BUILD_REVISION=old","Jwt__SecretKey=original-test-key"],"Labels":{"com.docker.compose.service":"nesthub"}},
        "HostConfig":{"AutoRemove":false,"Binds":[],"PortBindings":{"8080/tcp":[{"HostIp":"0.0.0.0","HostPort":"5186"}]}},
        "Mounts":[{"Type":"volume","Name":"original-volume","Destination":"/app/AppData","RW":true}],
        "NetworkSettings":{"Networks":{"nesthub_default":{"Aliases":["nesthub"],"IPAddress":"172.18.0.2"}}}}
        """;
        else if (path.StartsWith("/images/create")) result = mode == "pull-failure" ? """{"error":"denied"}""" : """{"status":"complete"}""";
        else if (path.StartsWith("/images/")) result = new JsonObject {
            ["Id"] = mode == "unchanged" ? "sha256:old" : "sha256:new",
            ["Config"] = new JsonObject { ["Env"] = new JsonArray("NESTHUB_BUILD_REVISION=" + new string('b', 40)), ["Healthcheck"] = new JsonObject { ["Test"] = new JsonArray("CMD", "true") } }
        }.ToJsonString();
        else if (path.StartsWith("/containers/create"))
        {
            if (body?["Cmd"]?[0]?.GetValue<string>() == "--self-update")
            { CheckWorker(body); result = """{"Id":"worker"}"""; }
            else { Replacement = body; result = """{"Id":"replacement"}"""; }
        }
        else if (path == "/containers/replacement/json") result = mode == "health-failure" ? """{"State":{"Running":false}}""" : """{"State":{"Running":true,"Health":{"Status":"healthy"}}}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(result) };
    }
    private static void CheckWorker(JsonNode body)
    {
        if (body["HostConfig"]?["AutoRemove"]?.GetValue<bool>() != true || body["Image"]!.GetValue<string>() != "sha256:old") throw new Exception("Worker must use local app image and remove itself");
    }
}

sealed class UpdateHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"workflow_runs\":[{\"head_sha\":\"" + new string('b', 40) + "\",\"updated_at\":\"2026-10-10T00:00:00Z\"}]}")
        });
    }
}
