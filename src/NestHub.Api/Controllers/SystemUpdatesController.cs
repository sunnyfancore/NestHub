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
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        if (tenant.Current?.IsSuperAdmin != true) return Forbid();
        try { return Ok(await updates.CheckAsync(cancellationToken)); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(503, new { message = "暂时无法查询 GitHub 更新，请稍后重试。" }); }
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(503, new { message = "更新服务未响应，请检查 Docker 更新服务是否启动。" }); }
    }
}
