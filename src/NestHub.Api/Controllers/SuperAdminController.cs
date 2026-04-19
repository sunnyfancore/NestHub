using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/super")]
public sealed class SuperAdminController : ControllerBase
{
    private readonly SuperAdminService _superAdminService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public SuperAdminController(SuperAdminService superAdminService, TenantContextAccessor tenantContextAccessor)
    {
        _superAdminService = superAdminService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    private bool IsSuperAdmin()
    {
        return _tenantContextAccessor.Current?.IsSuperAdmin == true;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants()
    {
        if (!IsSuperAdmin()) return Forbid();
        return Ok(await _superAdminService.GetTenantsAsync());
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();
        try
        {
            return Ok(await _superAdminService.CreateTenantAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("tenants/{id:guid}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();
        try
        {
            await _superAdminService.UpdateTenantAsync(id, request.Name, request.DisplayName);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("tenants/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();
        try
        {
            await _superAdminService.ToggleTenantActiveAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("tenants/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();
        try
        {
            await _superAdminService.ResetPasswordAsync(id, request.NewPassword);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
