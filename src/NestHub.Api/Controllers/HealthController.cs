using Microsoft.AspNetCore.Mvc;

namespace NestHub.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "NestHub.Api",
            utcNow = DateTime.UtcNow
        });
    }
}
