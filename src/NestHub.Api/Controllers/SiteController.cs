using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Contracts.Portal;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/site")]
public sealed class SiteController : ControllerBase
{
    private readonly SiteSettingService _siteSettingService;
    private readonly TenantContextAccessor _tenantContextAccessor;

    public SiteController(SiteSettingService siteSettingService, TenantContextAccessor tenantContextAccessor)
    {
        _siteSettingService = siteSettingService;
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

        return Ok(await _siteSettingService.GetAsync(tenantContext));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SaveSiteSettingsRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _siteSettingService.UpdateAsync(request, tenantContext));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("search-engine")]
    public async Task<IActionResult> UpdateSearchEngine([FromBody] UpdateSearchEngineRequest request)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        try
        {
            await _siteSettingService.UpdateSearchEngineAsync(request.SearchEngine, tenantContext);
            return Ok(new { searchEngine = request.SearchEngine });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("upload-logo")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadLogo([FromForm] IFormFile file)
    {
        var tenantContext = _tenantContextAccessor.Current;
        if (tenantContext is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "请选择图片文件。" });
        }

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/svg+xml", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "仅支持 PNG、JPG、GIF、SVG、WebP 格式。" });
        }

        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp" };
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "仅支持 PNG、JPG、GIF、SVG、WebP 格式。" });
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "AppData", "uploads", "logos");
        Directory.CreateDirectory(uploadsDir);

        var prefix = $"{tenantContext.TenantId}.";
        foreach (var oldFile in Directory.GetFiles(uploadsDir, $"{prefix}*"))
        {
            try { System.IO.File.Delete(oldFile); } catch { /* ignore */ }
        }

        var fileName = $"{tenantContext.TenantId}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var logoUrl = $"/uploads/logos/{fileName}";

        await _siteSettingService.UpdateLogoUrlAsync(tenantContext, logoUrl);

        return Ok(new { logoUrl });
    }
}
