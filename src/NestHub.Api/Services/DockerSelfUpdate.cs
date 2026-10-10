using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace NestHub.Api.Services;

public sealed class DockerEngineClient : IDisposable
{
    private readonly HttpClient _client;
    public DockerEngineClient(string socketPath, HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new SocketsHttpHandler
        {
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token); return new NetworkStream(socket, true); }
                catch { socket.Dispose(); throw; }
            }
        }) { BaseAddress = new Uri("http://docker/"), Timeout = TimeSpan.FromMinutes(15) };
    }
    public async Task<JsonNode?> RequestAsync(HttpMethod method, string path, JsonNode? body = null, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };
        using var response = await _client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Docker 操作失败（{(int)response.StatusCode}），请检查容器配置与 Docker 服务日志。");
        var text = await response.Content.ReadAsStringAsync(token);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
    public async Task PullAsync(string image, CancellationToken token)
    {
        using var response = await _client.PostAsync("images/create?fromImage=" + Uri.EscapeDataString(image), null, token);
        response.EnsureSuccessStatusCode();
        // Docker can report a pull error inside a successful streaming HTTP response.
        foreach (var line in (await response.Content.ReadAsStringAsync(token)).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            if (JsonNode.Parse(line)?["error"] is not null) throw new InvalidOperationException("镜像拉取失败，请检查镜像地址、代理同步及仓库访问权限。");
    }
    public void Dispose() => _client.Dispose();
}

public static class DockerSelfUpdate
{
    public const string SocketPath = "/var/run/docker.sock";
    public const string ResultPath = "/app/AppData/update-status.json";
    public static JsonObject CreateReplacement(JsonObject inspect, JsonObject newImage)
    {
        var config = (JsonObject)inspect["Config"]!.DeepClone();
        var oldEnv = config["Env"]!.AsArray().Select(e => e!.GetValue<string>()).ToArray();
        // Image build metadata must come from the newly pulled image, never from the old container.
        var imageEnv = newImage["Config"]?["Env"]?.AsArray().Select(e => e!.GetValue<string>()) ?? [];
        config["Env"] = new JsonArray(oldEnv.Where(e => !e.StartsWith("NESTHUB_BUILD_REVISION=")).Concat(imageEnv.Where(e => e.StartsWith("NESTHUB_BUILD_REVISION="))).Select(e => (JsonNode?)JsonValue.Create(e)).ToArray());
        config["Image"] = inspect["Config"]!["Image"]!.DeepClone();
        config["Hostname"] = "";
        config["Healthcheck"] = inspect["Config"]?["Healthcheck"]?.DeepClone() ?? newImage["Config"]?["Healthcheck"]?.DeepClone();
        var host = (JsonObject)inspect["HostConfig"]!.DeepClone();
        // Preserve named/anonymous volumes explicitly; otherwise anonymous volumes could be recreated empty.
        var binds = host["Binds"]?.AsArray() ?? new JsonArray();
        foreach (var mount in inspect["Mounts"]?.AsArray() ?? [])
        {
            var destination = mount?["Destination"]?.GetValue<string>();
            if (mount?["Type"]?.GetValue<string>() != "volume" || destination is null) continue;
            var structured = host["Mounts"]?.AsArray().FirstOrDefault(m => m?["Target"]?.GetValue<string>() == destination);
            if (structured is not null)
            {
                if (structured["Type"]?.GetValue<string>() == "volume" && string.IsNullOrWhiteSpace(structured["Source"]?.GetValue<string>()))
                    structured["Source"] = mount!["Name"]!.DeepClone();
                continue;
            }
            if (!binds.Any(b => b!.GetValue<string>().Split(':').Skip(1).FirstOrDefault() == destination))
                binds.Add($"{mount!["Name"]!.GetValue<string>()}:{destination}:{(mount["RW"]?.GetValue<bool>() == false ? "ro" : "rw")}");
        }
        host["Binds"] = binds.DeepClone();
        config["HostConfig"] = host;
        config["NetworkingConfig"] = new JsonObject { ["EndpointsConfig"] = NetworkEndpoints(inspect) };
        return config;
    }
    public static JsonObject NetworkEndpoints(JsonObject inspect)
    {
        var endpoints = new JsonObject();
        if (inspect["HostConfig"]?["NetworkMode"]?.GetValue<string>() is "host" or "none") return endpoints;
        foreach (var pair in inspect["NetworkSettings"]?["Networks"]?.AsObject() ?? new JsonObject())
        {
            var original = pair.Value!;
            // Do not pin dynamically allocated addresses or copy stale endpoint IDs.
            endpoints[pair.Key] = new JsonObject { ["Aliases"] = original["Aliases"]?.DeepClone(), ["IPAMConfig"] = original["IPAMConfig"]?.DeepClone(), ["DriverOpts"] = original["DriverOpts"]?.DeepClone() };
        }
        return endpoints;
    }
    public static async Task<string> LaunchAsync(DockerEngineClient docker, string containerName, CancellationToken token)
    {
        var old = (JsonObject)(await docker.RequestAsync(HttpMethod.Get, $"containers/{Uri.EscapeDataString(containerName)}/json", token: token))!;
        if (old["State"]?["Running"]?.GetValue<bool>() != true) throw new InvalidOperationException("当前应用容器未运行，无法启动更新任务。");
        if (old["HostConfig"]?["AutoRemove"]?.GetValue<bool>() == true) throw new InvalidOperationException("自动删除容器无法保证失败回退，请先关闭 auto_remove。");
        if (old["HostConfig"]?["NetworkMode"]?.GetValue<string>().StartsWith("container:") == true)
            throw new InvalidOperationException("共享其他容器网络命名空间的部署不支持内置替换，请改用 Compose 网络。");
        var name = old["Name"]!.GetValue<string>().TrimStart('/');
        var taskName = name + "-update-task";
        var worker = new JsonObject
        {
            ["Image"] = old["Image"]!.DeepClone(), ["User"] = "0:0",
            ["Entrypoint"] = new JsonArray("dotnet", "/app/NestHub.Api.dll"), ["Cmd"] = new JsonArray("--self-update"),
            ["Env"] = new JsonArray("NESTHUB_UPDATE_TARGET=" + old["Id"]!.GetValue<string>()),
            ["HostConfig"] = new JsonObject { ["AutoRemove"] = true, ["NetworkMode"] = "none", ["Binds"] = new JsonArray(SocketPath + ":" + SocketPath, 
                // The task status must survive replacement and be visible to the new app.
                StatusBind(old)) }
        };
        var created = await docker.RequestAsync(HttpMethod.Post, "containers/create?name=" + Uri.EscapeDataString(taskName), worker, token);
        var id = created!["Id"]!.GetValue<string>();
        try { await docker.RequestAsync(HttpMethod.Post, "containers/" + id + "/start", token: token); }
        catch { await docker.RequestAsync(HttpMethod.Delete, "containers/" + id + "?force=true", token: CancellationToken.None); throw; }
        return id;
    }
    private static string StatusBind(JsonObject inspect)
    {
        var mount = inspect["Mounts"]!.AsArray().FirstOrDefault(m => m?["Destination"]?.GetValue<string>() == "/app/AppData");
        if (mount is null || mount["RW"]?.GetValue<bool>() != true) throw new InvalidOperationException("后台更新需要可写的 /app/AppData 数据挂载。");
        return (mount["Type"]?.GetValue<string>() == "volume" ? mount["Name"] : mount["Source"])!.GetValue<string>() + ":/app/AppData";
    }
    private static async Task ReportAsync(string path, string state, string message)
    {
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, new JsonObject { ["state"] = state, ["message"] = message, ["updatedAt"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        File.Move(temp, path, true);
    }
    public static async Task<int> RunAsync(DockerEngineClient? supplied = null, string? target = null, string? resultPath = null, Func<Task>? wait = null)
    {
        using var docker = supplied ?? new DockerEngineClient(SocketPath);
        var reportPath = resultPath ?? ResultPath;
        var id = target ?? Environment.GetEnvironmentVariable("NESTHUB_UPDATE_TARGET") ?? throw new InvalidOperationException("Missing update target");
        JsonObject? old = null; string? replacementId = null; string? name = null;
        var stopped = false; var renamed = false; var disconnected = new List<string>();
        try
        {
            old = (JsonObject)(await docker.RequestAsync(HttpMethod.Get, "containers/" + id + "/json"))!;
            name = old["Name"]!.GetValue<string>().TrimStart('/');
            var image = old["Config"]!["Image"]!.GetValue<string>();
            await ReportAsync(reportPath, "pulling", "正在拉取镜像，应用保持运行。");
            await docker.PullAsync(image, CancellationToken.None);
            var newImage = (JsonObject)(await docker.RequestAsync(HttpMethod.Get, "images/" + Uri.EscapeDataString(image) + "/json"))!;
            if (newImage["Id"]!.GetValue<string>() == old["Image"]!.GetValue<string>())
            { await ReportAsync(reportPath, "unchanged", "镜像仓库尚无更新；使用代理地址时请等待代理同步后重试。"); return 0; }
            var config = CreateReplacement(old, newImage);
            await ReportAsync(reportPath, "replacing", "正在替换应用容器并保留数据与部署配置。");
            await docker.RequestAsync(HttpMethod.Post, "containers/" + id + "/stop?t=30"); stopped = true;
            await docker.RequestAsync(HttpMethod.Post, "containers/" + id + "/rename?name=" + Uri.EscapeDataString(name + "-rollback-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds())); renamed = true;
            foreach (var network in NetworkEndpoints(old))
            {
                await docker.RequestAsync(HttpMethod.Post, "networks/" + Uri.EscapeDataString(network.Key) + "/disconnect", new JsonObject { ["Container"] = id, ["Force"] = true });
                disconnected.Add(network.Key);
            }
            var created = await docker.RequestAsync(HttpMethod.Post, "containers/create?name=" + Uri.EscapeDataString(name), config);
            replacementId = created!["Id"]!.GetValue<string>();
            await docker.RequestAsync(HttpMethod.Post, "containers/" + replacementId + "/start");
            var healthy = false;
            for (var attempt = 0; attempt < 90; attempt++)
            {
                await (wait?.Invoke() ?? Task.Delay(2000));
                var state = (await docker.RequestAsync(HttpMethod.Get, "containers/" + replacementId + "/json"))!["State"]!;
                if (state["Running"]?.GetValue<bool>() != true) break;
                if (state["Health"]?["Status"]?.GetValue<string>() == "healthy") { healthy = true; break; }
                if (state["Health"]?["Status"]?.GetValue<string>() == "unhealthy") break;
            }
            if (!healthy) throw new InvalidOperationException("新版容器未通过健康检查。");
            await ReportAsync(reportPath, "success", "更新完成，已通过健康检查。");
            // Do not remove volumes; keep old image available for manual rollback.
            try { await docker.RequestAsync(HttpMethod.Delete, "containers/" + id); } catch { /* A stopped rollback container can be cleaned up manually. */ }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("NestHub 更新任务失败：" + ex.Message);
            try
            {
                if (replacementId is not null) await docker.RequestAsync(HttpMethod.Delete, "containers/" + replacementId + "?force=true");
                if (renamed) await docker.RequestAsync(HttpMethod.Post, "containers/" + id + "/rename?name=" + Uri.EscapeDataString(name!));
                if (old is not null) foreach (var network in disconnected)
                    await docker.RequestAsync(HttpMethod.Post, "networks/" + Uri.EscapeDataString(network) + "/connect", new JsonObject { ["Container"] = id, ["EndpointConfig"] = NetworkEndpoints(old)[network]!.DeepClone() });
                if (stopped) await docker.RequestAsync(HttpMethod.Post, "containers/" + id + "/start");
                await ReportAsync(reportPath, "failed", stopped ? "更新失败，已尝试恢复旧容器。请检查 Docker 日志；数据库结构变更不保证可回退。" : "镜像拉取或更新准备失败，原应用继续运行。请检查镜像地址和 Docker 日志。");
            }
            catch { await ReportAsync(reportPath, "failed", "更新失败且旧容器未能恢复，请在部署目录执行 docker compose up -d 并检查 Docker 日志。"); }
            return 1;
        }
    }
}
