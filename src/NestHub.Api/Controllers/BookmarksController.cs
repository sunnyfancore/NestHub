using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Bookmarks;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bookmarks")]
public sealed class BookmarksController : ControllerBase
{
    private readonly BookmarkService _bookmarkService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public BookmarksController(
        BookmarkService bookmarkService,
        TenantContextAccessor tenantContextAccessor)
    {
        _bookmarkService = bookmarkService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] BookmarkQueryRequest request)
    {
        return Ok(await _bookmarkService.GetPagedListAsync(request));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveBookmarkRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _bookmarkService.CreateAsync(request, tenantContext));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveBookmarkRequest request)
    {
        try
        {
            return Ok(await _bookmarkService.UpdateAsync(id, request));
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
            await _bookmarkService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/open")]
    public async Task<IActionResult> Open(Guid id)
    {
        try
        {
            await _bookmarkService.OpenAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
