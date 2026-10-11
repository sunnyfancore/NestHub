using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/system-updates")]
public sealed class SystemUpdatesController(SystemUpdateService updates, TenantContextAccessor tenant) : ControllerBase
{
    [HttpGet("network")]
    public IActionResult Network()
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { return Ok(updates.GetNetworkSettings()); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        { return BadRequest(new { message = "无法读取下载网络设置，请检查数据目录。" }); }
    }

    [HttpPut("network")]
    public async Task<IActionResult> SaveNetwork(SystemUpdateService.NetworkSettings settings, CancellationToken token)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { await updates.SaveNetworkSettingsAsync(settings, token); return Ok(new { message = "下载网络已保存，立即生效。" }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return BadRequest(new { message = "无法保存，请检查 AppData 写入权限。" }); }
    }

    [HttpPost("network/test")]
    public async Task<IActionResult> TestNetwork(SystemUpdateService.NetworkSettings settings, CancellationToken token)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { await updates.TestNetworkAsync(settings, token); return Ok(new { message = "GitHub 版本查询、校验清单与应用包连接正常。" }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or System.Text.Json.JsonException)
        { return BadRequest(new { message = "连接测试失败，请确认服务器能访问 GitHub API、清单和应用包。加速地址只加速附件，API 不通时请配置网络代理。" }); }
    }

    [HttpGet]
    public async Task<IActionResult> Check(CancellationToken cancellationToken, [FromQuery] bool force = false)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { return Ok((await updates.CheckAsync(cancellationToken, force)) with { ServerTime = DateTimeOffset.UtcNow.ToString("O") }); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(503, new { message = "暂时无法查询 GitHub 更新，请稍后重试。" }); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException)
        { return StatusCode(503, new { message = "发布包信息不完整或运行环境不兼容，请稍后重试或更新基础镜像。" }); }
    }

    [HttpGet("progress")]
    public IActionResult Progress()
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        return Ok(updates.Progress() with { ServerTime = DateTimeOffset.UtcNow.ToString("O") });
    }

    [HttpPost]
    public async Task<IActionResult> Apply(CancellationToken cancellationToken)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try
        {
            await updates.ApplyAsync(cancellationToken);
            return Accepted(new { message = "更新请求已发送，应用可能短暂断开，请等待恢复。" });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (HttpRequestException)
        { return StatusCode(503, new { message = "无法查询应用包，请检查网络或下载代理。" }); }
        catch (TaskCanceledException)
        { return StatusCode(503, new { message = "查询 GitHub 应用包超时，尚未开始更新。请检查服务器网络或配置 Updates__ProxyUrl 后重试。" }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return BadRequest(new { message = "应用包信息无效，或数据目录不可写，请检查部署配置。" }); }
    }
}
