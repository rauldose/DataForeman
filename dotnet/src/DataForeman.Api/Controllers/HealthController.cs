using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        
        return Ok(new
        {
            status = report.Status.ToString(),
            service = "dataforeman-api",
            version = "0.4.3",
            uptime = GetUptime(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration_ms = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data
                })
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var report = await _healthCheckService.CheckHealthAsync(
            predicate: check => check.Tags.Contains("ready"));
        
        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, new { status = report.Status.ToString() });
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "live" });
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}

