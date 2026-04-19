using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Route("api/portal")]
public sealed class PortalController : ControllerBase
{
    private readonly PortalService _portalService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public PortalController(PortalService portalService, TenantContextAccessor tenantContextAccessor)
    {
        _portalService = portalService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] bool? publicView = null)
    {
        try
        {
            return Ok(await _portalService.GetPortalAsync(_tenantContextAccessor.Current, publicView));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] bool? publicView = null)
    {
        return Ok(await _portalService.SearchAsync(keyword, _tenantContextAccessor.Current, publicView));
    }

    [HttpPost("links/{id:guid}/click")]
    [AllowAnonymous]
    public async Task<IActionResult> Click(Guid id)
    {
        try
        {
            await _portalService.RecordClickAsync(id, _tenantContextAccessor.Current);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
