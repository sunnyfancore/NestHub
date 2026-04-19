using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/links")]
public sealed class LinksController : ControllerBase
{
    private readonly NavigationLinkService _navigationLinkService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public LinksController(NavigationLinkService navigationLinkService, TenantContextAccessor tenantContextAccessor)
    {
        _navigationLinkService = navigationLinkService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePortalLinkRequest request, [FromQuery] Guid? targetTenantId = null)
    {
        var ctx = ResolveContext(targetTenantId);
        if (ctx is null) return Unauthorized();

        try
        {
            return Ok(await _navigationLinkService.CreateAsync(request, ctx));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePortalLinkRequest request)
    {
        try
        {
            return Ok(await _navigationLinkService.UpdateAsync(id, request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _navigationLinkService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("sort")]
    public async Task<IActionResult> Sort([FromBody] SortRequest request)
    {
        try
        {
            await _navigationLinkService.UpdateOrderAsync(request.OrderedIds);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private TenantContext? ResolveContext(Guid? targetTenantId)
    {
        var ctx = _tenantContextAccessor.Current;
        if (ctx is null) return null;

        if (targetTenantId.HasValue && ctx.IsSuperAdmin)
        {
            return ctx with { TenantId = targetTenantId.Value };
        }

        return ctx;
    }
}
