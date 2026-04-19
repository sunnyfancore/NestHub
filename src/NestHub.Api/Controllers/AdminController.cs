using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Admin;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly BookmarkHtmlGenerator _bookmarkHtmlGenerator;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public AdminController(AdminService adminService, BookmarkHtmlGenerator bookmarkHtmlGenerator, TenantContextAccessor tenantContextAccessor)
    {
        _adminService = adminService;
        _bookmarkHtmlGenerator = bookmarkHtmlGenerator;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] Guid? targetTenantId = null)
    {
        var tenantContext = ResolveContext(targetTenantId);
        if (tenantContext is null) return Unauthorized();
        return Ok(await _adminService.GetCategoriesAsync(tenantContext));
    }

    [HttpGet("links")]
    public async Task<IActionResult> GetLinks([FromQuery] string? keyword = null, [FromQuery] Guid? categoryId = null, [FromQuery] Guid? targetTenantId = null)
    {
        var tenantContext = ResolveContext(targetTenantId);
        if (tenantContext is null) return Unauthorized();
        return Ok(await _adminService.GetLinksAsync(tenantContext, keyword, categoryId));
    }

    [HttpGet("themes")]
    public IActionResult GetThemes()
    {
        return Ok(_adminService.GetThemes());
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import([FromForm] BookmarkImportRequest request, [FromQuery] Guid? targetTenantId = null)
    {
        var tenantContext = ResolveContext(targetTenantId);
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { message = "请选择要导入的书签文件。" });
        }

        try
        {
            await using var stream = request.File.OpenReadStream();
            return Ok(await _adminService.ImportBookmarksAsync(request.CategoryId, stream, tenantContext));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid? targetTenantId = null)
    {
        var tenantContext = ResolveContext(targetTenantId);
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        var payload = await _adminService.ExportAsync(tenantContext);
        return Ok(payload);
    }

    [HttpGet("export-bookmarks")]
    public async Task<IActionResult> ExportBookmarks([FromQuery] Guid? targetTenantId = null)
    {
        var tenantContext = ResolveContext(targetTenantId);
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        var html = await _adminService.ExportBookmarkHtmlAsync(tenantContext, _bookmarkHtmlGenerator);
        var bytes = Encoding.UTF8.GetBytes(html);
        var fileName = $"bookmarks_{DateTime.UtcNow:yyyyMMdd}.html";

        return File(bytes, "text/html; charset=utf-8", fileName);
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
