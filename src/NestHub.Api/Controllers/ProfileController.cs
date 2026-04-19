using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public ProfileController(
        ProfileService profileService,
        TenantContextAccessor tenantContextAccessor)
    {
        _profileService = profileService;
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

        return Ok(await _profileService.GetAsync(tenantContext));
    }
}
