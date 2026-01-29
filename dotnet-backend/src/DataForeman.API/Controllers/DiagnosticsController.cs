using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

/// <summary>
/// Controller for system diagnostics and health monitoring.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<DiagnosticsController> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public DiagnosticsController(DataForemanDbContext context, ILogger<DiagnosticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get system health status.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var dbHealthy = await CheckDatabaseHealth();

        return Ok(new
        {
            status = dbHealthy ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            uptime = DateTime.UtcNow - _startTime,
            checks = new
            {
                database = dbHealthy ? "healthy" : "unhealthy"
            }
        });
    }

    /// <summary>
    /// Get system metrics.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var process = Process.GetCurrentProcess();

        // Database stats
        var userCount = await _context.Users.CountAsync();
        var dashboardCount = await _context.Dashboards.Where(d => !d.IsDeleted).CountAsync();
        var flowCount = await _context.Flows.CountAsync();
        var connectionCount = await _context.Connections.Where(c => c.DeletedAt == null).CountAsync();
        var tagCount = await _context.TagMetadata.Where(t => !t.IsDeleted).CountAsync();

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            uptime = new
            {
                seconds = (DateTime.UtcNow - _startTime).TotalSeconds,
                formatted = FormatUptime(DateTime.UtcNow - _startTime)
            },
            process = new
            {
                pid = process.Id,
                memoryMb = process.WorkingSet64 / 1024 / 1024,
                threads = process.Threads.Count
            },
            runtime = new
            {
                dotnetVersion = RuntimeInformation.FrameworkDescription,
                osDescription = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString()
            },
            database = new
            {
                users = userCount,
                dashboards = dashboardCount,
                flows = flowCount,
                connections = connectionCount,
                tags = tagCount
            }
        });
    }

    /// <summary>
    /// Get detailed system info.
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        var assembly = typeof(DiagnosticsController).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "unknown";

        return Ok(new
        {
            application = "DataForeman",
            version,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            runtime = new
            {
                dotnetVersion = RuntimeInformation.FrameworkDescription,
                osDescription = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                processorCount = Environment.ProcessorCount
            },
            startTime = _startTime,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get audit log events.
    /// </summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.AuditEvents.AsQueryable();

        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(e => e.Action.Contains(action));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Timestamp <= to.Value);
        }

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(offset)
            .Take(Math.Min(limit, 200))
            .Select(e => new
            {
                e.Id,
                e.Action,
                e.Outcome,
                e.ActorUserId,
                e.IpAddress,
                e.TargetType,
                e.TargetId,
                e.Timestamp
            })
            .ToListAsync();

        return Ok(new { items = events, limit, offset, count = events.Count });
    }

    /// <summary>
    /// Get system settings.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _context.SystemSettings.ToListAsync();

        var result = settings.ToDictionary(
            s => s.Key,
            s => s.Value
        );

        return Ok(new { settings = result });
    }

    /// <summary>
    /// Update a system setting.
    /// </summary>
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        var setting = await _context.SystemSettings.FindAsync(key);

        if (setting == null)
        {
            setting = new Core.Entities.SystemSetting
            {
                Key = key,
                Value = request.Value
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("System setting {Key} updated", key);

        return Ok(new { ok = true });
    }

    private async Task<bool> CheckDatabaseHealth()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }
        return $"{(int)uptime.TotalSeconds}s";
    }
}

public record UpdateSettingRequest(string Value);
