using Microsoft.AspNetCore.Mvc;

namespace Ejar.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", time = DateTime.UtcNow, service = "Ejar.Api" });
}
