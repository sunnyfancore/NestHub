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
    [HttpGet]
    public async Task<IActionResult> Check(CancellationToken cancellationToken, [FromQuery] bool force = false)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { return Ok(await updates.CheckAsync(cancellationToken, force)); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(503, new { message = "暂时无法查询 GitHub 更新，请稍后重试。" }); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException)
        { return StatusCode(503, new { message = "发布包信息不完整或运行环境不兼容，请稍后重试或更新基础镜像。" }); }
    }

    [HttpGet("progress")]
    public IActionResult Progress()
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        return Ok(updates.Progress());
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
        { return StatusCode(503, new { message = "查询应用包超时，请稍后重试。" }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return BadRequest(new { message = "应用包信息无效，或数据目录不可写，请检查部署配置。" }); }
    }
}
