using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Folders;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/folders")]
public sealed class FoldersController : ControllerBase
{
    private readonly FolderService _folderService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public FoldersController(
        FolderService folderService,
        TenantContextAccessor tenantContextAccessor)
    {
        _folderService = folderService;
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

        return Ok(await _folderService.GetListAsync(tenantContext));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveFolderRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _folderService.CreateAsync(request, tenantContext));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveFolderRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _folderService.UpdateAsync(id, request, tenantContext));
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
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            await _folderService.DeleteAsync(id, tenantContext);
            return NoContent();
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
}
