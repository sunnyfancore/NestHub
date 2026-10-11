using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestHub.Api.Services;

public sealed record SystemUpdateStatus(string CurrentRevision, string LatestRevision, string? PublishedAt,
    bool HasUpdate, bool UpdateEnabled, string WorkflowUrl, string? DisabledReason = null, string? UpdateState = null, string? UpdateMessage = null, string? UpdateTaskUpdatedAt = null, string? UpdateStartedAt = null, long? DownloadedBytes = null, long? TotalBytes = null, string? UpdateStage = null);

public sealed class SystemUpdateService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _client;
    private readonly IHostApplicationLifetime? _lifetime;
    private readonly SemaphoreSlim _gate = new(1);
    private SystemUpdateStatus? _cached;
    private DateTime _checkedAt;
    private Task? _task;
    private JsonObject? _manifest;
    private const string Repository = "https://github.com/sunnyfancore/NestHub";
    public string Root => Path.Combine(Path.GetFullPath(_configuration["Updates:DataPath"] ?? "AppData"), "updates");
    private string ResultPath => Path.Combine(Root, "status.json");
    private string Current => _configuration["NESTHUB_BUILD_REVISION"] ?? "local";
    private string Rid => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    public SystemUpdateService(IConfiguration configuration, IHostApplicationLifetime? lifetime = null, HttpClient? client = null)
    {
        _configuration = configuration; _lifetime = lifetime;
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(configuration["Updates:ProxyUrl"])) handler.Proxy = new System.Net.WebProxy(configuration["Updates:ProxyUrl"]!);
        _client = client ?? new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NestHub-Updates/2.0");
    }
    public string? DisabledReason => !_configuration.GetValue<bool>("Updates:Enabled") ? "请设置 Updates__Enabled=true 启用应用包更新。" :
        _configuration["NESTHUB_APP_LAUNCHER"] != "1" ? "当前运行环境尚未安装应用更新启动器，请先更新一次基础镜像。" :
        !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64) ? "应用包更新目前支持 Linux x64 和 ARM64。" : null;
    public bool Enabled => DisabledReason is null;
    public async Task<SystemUpdateStatus> CheckAsync(CancellationToken token, bool force = false)
    {
        if (!force && _cached is not null && DateTime.UtcNow - _checkedAt < TimeSpan.FromSeconds(30)) return WithStatus(_cached);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var text = await _client.GetStringAsync("https://api.github.com/repos/sunnyfancore/NestHub/releases?per_page=30", timeout.Token);
        var releases = JsonNode.Parse(text)!.AsArray();
        _manifest = null;
        foreach (var release in releases.Where(r => r?["draft"]?.GetValue<bool>() == false && r?["tag_name"]?.GetValue<string>().StartsWith("app-") == true))
        {
            var asset = release?["assets"]?.AsArray().FirstOrDefault(a => a?["name"]?.GetValue<string>() == "manifest.json");
            if (asset is null) continue;
            var url = asset["browser_download_url"]!.GetValue<string>(); ValidateUrl(url);
            var manifest = JsonNode.Parse(await _client.GetStringAsync(url, timeout.Token))!.AsObject();
            ValidateManifest(manifest, Rid);
            _manifest = manifest; break;
        }
        var latest = _manifest?["revision"]?.GetValue<string>() ?? "";
        var hasUpdate = latest.Length == 40 && latest != Current;
        // Never offer an older application package while a newer base image is being published.
        if (hasUpdate && Current.Length == 40 && Current.All(Uri.IsHexDigit))
        {
            var comparison = JsonNode.Parse(await _client.GetStringAsync($"https://api.github.com/repos/sunnyfancore/NestHub/compare/{Current}...{latest}", timeout.Token));
            hasUpdate = comparison?["status"]?.GetValue<string>() == "ahead";
        }
        _cached = new(Current, latest, _manifest?["publishedAt"]?.GetValue<string>(), hasUpdate, Enabled, Repository + "/releases", DisabledReason);
        _checkedAt = DateTime.UtcNow;
        return WithStatus(_cached);
    }
    public async Task ApplyAsync(CancellationToken token)
    {
        if (!Enabled) throw new InvalidOperationException(DisabledReason);
        if (!await _gate.WaitAsync(0, token)) throw new InvalidOperationException("已有更新任务正在执行。");
        try
        {
            if (_task is { IsCompleted: false } || File.Exists(Path.Combine(Root, "pending.json"))) throw new InvalidOperationException("已有更新任务正在执行。");
            var status = await CheckAsync(token, true);
            if (!status.HasUpdate || _manifest is null) throw new InvalidOperationException("没有可用的新应用包。");
            Directory.CreateDirectory(Root);
            var probe = Path.Combine(Root, ".write-test"); File.WriteAllText(probe, ""); File.Delete(probe);
            var manifest = (JsonObject)_manifest.DeepClone();
            var started = DateTime.UtcNow.ToString("O");
            Report("pulling", "正在下载应用包，网站仍可正常使用。", started);
            _task = Task.Run(() => InstallAsync(manifest, started));
        }
        finally { _gate.Release(); }
    }
    private async Task InstallAsync(JsonObject manifest, string started)
    {
        var staging = Path.Combine(Root, "staging-" + Guid.NewGuid().ToString("N"));
        var archive = staging + ".zip";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            var asset = manifest["packages"]![Rid]!.AsObject();
            using var response = await _client.GetAsync(asset["url"]!.GetValue<string>(), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            var expectedSize = asset["size"]!.GetValue<long>();
            await using (var output = File.Create(archive))
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            {
                var buffer = new byte[81920]; long count = 0; var last = DateTime.MinValue;
                int n; while ((n = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    count += n; if (count > expectedSize) throw new InvalidDataException("应用包大小不匹配。");
                    await output.WriteAsync(buffer.AsMemory(0, n), timeout.Token);
                    if (DateTime.UtcNow - last > TimeSpan.FromSeconds(1)) { Report("pulling", "正在下载应用包。", started, count, expectedSize); last = DateTime.UtcNow; }
                }
                if (count != expectedSize) throw new InvalidDataException("应用包下载不完整。");
            }
            Report("replacing", "正在校验应用包。", started);
            await using (var file = File.OpenRead(archive))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(file, timeout.Token)).Equals(asset["sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("应用包校验失败，未切换版本。");
            ExtractPackage(archive, staging);
            var runtime = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(staging, "NestHub.Api.runtimeconfig.json"), timeout.Token));
            if (runtime?["runtimeOptions"]?["tfm"]?.GetValue<string>() != "net10.0") throw new InvalidDataException("应用包要求不同的运行环境，请更新基础镜像。");
            var revision = manifest["revision"]!.GetValue<string>();
            var destination = Path.Combine(Root, "releases", revision); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.Move(staging, destination);
            Directory.CreateSymbolicLink(Path.Combine(destination, "AppData"), Path.GetDirectoryName(Root)!);
            AtomicWrite(Path.Combine(destination, "revision"), revision);
            AtomicWrite(Path.Combine(Root, "pending.json"), new JsonObject { ["revision"] = revision, ["startedAt"] = started }.ToJsonString());
            Report("checking", "应用包已就绪，正在重启并检查健康状态。", started);
            await Task.Delay(1500); _lifetime!.StopApplication();
        }
        catch (Exception ex) { Report("failed", ex is HttpRequestException or OperationCanceledException ? "下载失败或超时，请检查网络与下载代理后重试。" : ex.Message, started); }
        finally { if (File.Exists(archive)) File.Delete(archive); if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public static void ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith("/sunnyfancore/NestHub/releases/download/", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新地址不属于 NestHub 官方发布仓库。");
    }
    public static void ValidateManifest(JsonObject manifest, string rid)
    {
        var revision = manifest["revision"]?.GetValue<string>() ?? "";
        if (revision.Length != 40 || !revision.All(Uri.IsHexDigit) || manifest["runtime"]?.GetValue<string>() != "net10.0") throw new InvalidDataException("更新包版本或运行环境不兼容，请更新基础镜像。");
        var asset = manifest["packages"]?[rid]?.AsObject() ?? throw new InvalidDataException("没有适合当前架构的应用包。");
        ValidateUrl(asset["url"]!.GetValue<string>());
        var hash = asset["sha256"]!.GetValue<string>(); var size = asset["size"]!.GetValue<long>();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || size <= 0 || size > 500L * 1024 * 1024) throw new InvalidDataException("更新包校验信息无效。");
    }
    public static void ExtractPackage(string archive, string destination)
    {
        Directory.CreateDirectory(destination); long total = 0;
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Split('/').Any(p => p is ".." or "AppData") || name.Contains(':') || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("应用包包含不安全的路径。");
            total += entry.Length; if (total > 1024L * 1024 * 1024 || zip.Entries.Count > 20000) throw new InvalidDataException("应用包解压大小超限。");
            var path = Path.GetFullPath(Path.Combine(destination, name));
            if (!path.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("应用包路径越界。");
            if (name.EndsWith('/')) Directory.CreateDirectory(path);
            else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path); }
        }
        foreach (var required in new[] { "NestHub.Api.dll", "NestHub.Api.runtimeconfig.json", "wwwroot/index.html" })
            if (!File.Exists(Path.Combine(destination, required))) throw new InvalidDataException("应用包缺少必要文件。");
    }
    private static void AtomicWrite(string path, string text) { File.WriteAllText(path + ".tmp", text); File.Move(path + ".tmp", path, true); }
    private void Report(string state, string message, string started, long? downloaded = null, long? total = null) => AtomicWrite(ResultPath,
        new JsonObject { ["state"] = state, ["stage"] = state, ["message"] = message, ["startedAt"] = started, ["updatedAt"] = DateTime.UtcNow.ToString("O"), ["downloadedBytes"] = downloaded, ["totalBytes"] = total }.ToJsonString());
    public SystemUpdateStatus Progress() => WithStatus(new(Current, "", null, false, Enabled, Repository + "/releases", DisabledReason));
    private SystemUpdateStatus WithStatus(SystemUpdateStatus status)
    {
        try { var r = JsonNode.Parse(File.ReadAllText(ResultPath)); return status with { UpdateState = r?["state"]?.GetValue<string>(), UpdateStage = r?["stage"]?.GetValue<string>(), UpdateMessage = r?["message"]?.GetValue<string>(), UpdateStartedAt = r?["startedAt"]?.GetValue<string>(), UpdateTaskUpdatedAt = r?["updatedAt"]?.GetValue<string>(), DownloadedBytes = r?["downloadedBytes"]?.GetValue<long>(), TotalBytes = r?["totalBytes"]?.GetValue<long>() }; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return status; }
    }
    public void Dispose() { if (_task is null || _task.IsCompleted) _client.Dispose(); _gate.Dispose(); }
}
