using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/shares")]
public sealed class ShareController : ControllerBase
{
    private readonly ShareService _shareService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public ShareController(ShareService shareService, TenantContextAccessor tenantContextAccessor)
    {
        _shareService = shareService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        return Ok(await _shareService.GetListAsync(tenantContext));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveShareLinkRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _shareService.CreateAsync(request, tenantContext));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            await _shareService.DeleteAsync(id, tenantContext);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
