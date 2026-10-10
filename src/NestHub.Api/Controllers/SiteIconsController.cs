using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NestHub.Api.Services;

namespace NestHub.Api.Controllers;

[ApiController]
[Route("api/site-icons")]
public sealed class SiteIconsController(SiteIconService icons) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string url, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, max-age=600";
        return Ok(await icons.DiscoverAsync(url, cancellationToken));
    }
}
