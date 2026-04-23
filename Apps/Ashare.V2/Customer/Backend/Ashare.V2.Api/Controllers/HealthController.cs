using ACommerce.OperationEngine.Wire.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.V2.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check() =>
        this.OkEnvelope("health.check", new
        {
            status = "healthy",
            service = "Ashare.V2.Api",
            time = DateTime.UtcNow
        });
}
