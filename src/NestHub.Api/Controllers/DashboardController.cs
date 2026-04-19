using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public DashboardController(
        DashboardService dashboardService,
        TenantContextAccessor tenantContextAccessor)
    {
        _dashboardService = dashboardService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        return Ok(await _dashboardService.GetSummaryAsync(tenantContext));
    }
}
