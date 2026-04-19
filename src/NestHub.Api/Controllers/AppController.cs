using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/app-info")]
public sealed class AppController : ControllerBase
{
    private readonly AppInfoService _appInfoService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public AppController(AppInfoService appInfoService, TenantContextAccessor tenantContextAccessor)
    {
        _appInfoService = appInfoService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _appInfoService.GetAsync(tenantContext));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
