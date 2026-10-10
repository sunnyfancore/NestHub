using System.Text.Json.Nodes;
using System.Text.Json;

namespace NestHub.Api.Services;

public sealed record SystemUpdateStatus(string CurrentRevision, string LatestRevision, string? PublishedAt,
    bool HasUpdate, bool UpdateEnabled, string WorkflowUrl, string? DisabledReason = null, string? UpdateState = null, string? UpdateMessage = null, string? UpdateTaskUpdatedAt = null);

public sealed class SystemUpdateService(IConfiguration configuration, HttpClient? client = null, DockerEngineClient? docker = null) : IDisposable
{
    private readonly HttpClient _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    private readonly DockerEngineClient _docker = docker ?? new DockerEngineClient(DockerSelfUpdate.SocketPath);
    private readonly SemaphoreSlim _gate = new(1);
    private DateTime _lastRequest = DateTime.MinValue;
    private SystemUpdateStatus? _cached;
    private DateTime _checkedAt;

    public string? DisabledReason
    {
        get
        {
            if (!configuration.GetValue<bool>("Updates:Enabled")) return "当前容器未启用后台更新，请在 Compose 中设置 Updates__Enabled=true。";
            if (string.IsNullOrWhiteSpace(configuration["Updates:ContainerName"])) return "缺少当前容器名称，请在 Compose 中配置 container_name 和 Updates__ContainerName。";
            if (!File.Exists(DockerSelfUpdate.SocketPath) && docker is null) return "未挂载 Docker socket，请在 Compose 中挂载 /var/run/docker.sock 并确保容器有读写权限。";
            return null;
        }
    }
    public bool Enabled => DisabledReason is null;

    public async Task<SystemUpdateStatus> CheckAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null && DateTime.UtcNow - _checkedAt < TimeSpan.FromSeconds(30)) return WithTaskStatus(_cached);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/sunnyfancore/NestHub/actions/workflows/docker.yml/runs?branch=main&status=success&per_page=1");
        request.Headers.UserAgent.ParseAdd("NestHub-Updates/1.0");
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var current = configuration["NESTHUB_BUILD_REVISION"] ?? "";
        var run = json.RootElement.GetProperty("workflow_runs").EnumerateArray().FirstOrDefault();
        var latest = run.ValueKind == JsonValueKind.Object ? run.GetProperty("head_sha").GetString() ?? "" : "";
        var published = run.ValueKind == JsonValueKind.Object ? run.GetProperty("updated_at").GetString() : null;
        _cached = new(current, latest, published, current.Length == 40 && latest.Length == 40 && current != latest, Enabled,
            "https://github.com/sunnyfancore/NestHub/actions/workflows/docker.yml", DisabledReason);
        _checkedAt = DateTime.UtcNow;
        return WithTaskStatus(_cached);
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!Enabled) throw new InvalidOperationException(DisabledReason);
        if (!await _gate.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("更新请求正在处理，请稍后再试。");
        try
        {
            if (DateTime.UtcNow - _lastRequest < TimeSpan.FromMinutes(2)) throw new InvalidOperationException("已发送更新请求，请等待应用重启。");
            await DockerSelfUpdate.LaunchAsync(_docker, configuration["Updates:ContainerName"]!, cancellationToken);
            _lastRequest = DateTime.UtcNow;
        }
        finally { _gate.Release(); }
    }

    private static SystemUpdateStatus WithTaskStatus(SystemUpdateStatus status)
    {
        try
        {
            var report = JsonNode.Parse(File.ReadAllText(DockerSelfUpdate.ResultPath));
            return status with { UpdateState = report?["state"]?.GetValue<string>(), UpdateMessage = report?["message"]?.GetValue<string>(), UpdateTaskUpdatedAt = report?["updatedAt"]?.GetValue<string>() };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return status; }
    }
    public void Dispose() { _client.Dispose(); _docker.Dispose(); _gate.Dispose(); }
}
