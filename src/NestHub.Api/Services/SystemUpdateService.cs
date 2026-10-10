using System.Net.Http.Headers;
using System.Text.Json;

namespace NestHub.Api.Services;

public sealed record SystemUpdateStatus(string CurrentRevision, string LatestRevision, string? PublishedAt,
    bool HasUpdate, bool UpdateEnabled, string WorkflowUrl);

public sealed class SystemUpdateService(IConfiguration configuration, HttpClient? client = null) : IDisposable
{
    private readonly HttpClient _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim _gate = new(1);
    private DateTime _lastRequest = DateTime.MinValue;
    private SystemUpdateStatus? _cached;
    private DateTime _checkedAt;

    public bool Enabled => configuration.GetValue<bool>("Updates:Enabled")
        && !string.IsNullOrWhiteSpace(configuration["Updates:Token"])
        && Uri.TryCreate(configuration["Updates:ServiceUrl"], UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    public async Task<SystemUpdateStatus> CheckAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null && DateTime.UtcNow - _checkedAt < TimeSpan.FromSeconds(30)) return _cached;
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
            "https://github.com/sunnyfancore/NestHub/actions/workflows/docker.yml");
        _checkedAt = DateTime.UtcNow;
        return _cached;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!Enabled) throw new InvalidOperationException("请先启用 Docker 更新服务。");
        if (!await _gate.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("更新请求正在处理，请稍后再试。");
        try
        {
            if (DateTime.UtcNow - _lastRequest < TimeSpan.FromMinutes(2)) throw new InvalidOperationException("已发送更新请求，请等待应用重启。");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(configuration["Updates:ServiceUrl"]!), "/v1/update"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Updates:Token"]);
            using var response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _lastRequest = DateTime.UtcNow;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() { _client.Dispose(); _gate.Dispose(); }
}
