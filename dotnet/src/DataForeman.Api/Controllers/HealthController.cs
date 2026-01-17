using Microsoft.AspNetCore.Mvc;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", service = "dataforeman-api", version = "0.4.3" });
    }

    [HttpGet("ready")]
    public IActionResult Ready()
    {
        return Ok(new { status = "ready" });
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "live" });
    }
}
