using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transition")]
public sealed class TransitionController : ControllerBase
{
    private readonly TransitionSettingService _transitionSettingService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public TransitionController(TransitionSettingService transitionSettingService, TenantContextAccessor tenantContextAccessor)
    {
        _transitionSettingService = transitionSettingService;
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

        return Ok(await _transitionSettingService.GetAsync(tenantContext));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SaveTransitionSettingRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        return Ok(await _transitionSettingService.UpdateAsync(request, tenantContext));
    }
}
