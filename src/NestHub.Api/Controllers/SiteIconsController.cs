using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Route("api/site-icons")]
public sealed class SiteIconsController(SiteIconService icons) : ControllerBase
{
    [HttpGet("image")]
    [AllowAnonymous]
    public async Task<IActionResult> Image([FromQuery] string url, CancellationToken cancellationToken)
    {
        var image = await icons.LoadImageAsync(url, cancellationToken);
        Response.Headers.CacheControl = image is null ? "private, max-age=600" : "private, max-age=43200";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return image is null ? NotFound() : File(image.Bytes, image.ContentType);
    }
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string url, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, max-age=600";
        return Ok(await icons.DiscoverAsync(url, cancellationToken));
    }
}
