using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;

namespace DataForeman.Api.Services;

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DataForemanDbContext _context;

    public DatabaseHealthCheck(DataForemanDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to execute a simple query
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            
            var data = new Dictionary<string, object>
            {
                { "database", "SQLite" },
                { "connection_state", _context.Database.GetDbConnection().State.ToString() }
            };

            return HealthCheckResult.Healthy("Database is healthy", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}

/// <summary>
/// Memory health check
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _threshold;

    public MemoryHealthCheck(long thresholdMB = 500)
    {
        _threshold = thresholdMB * 1024 * 1024; // Convert to bytes
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        var data = new Dictionary<string, object>
        {
            { "allocated_mb", allocated / (1024 * 1024) },
            { "threshold_mb", _threshold / (1024 * 1024) },
            { "gc_gen0_collections", GC.CollectionCount(0) },
            { "gc_gen1_collections", GC.CollectionCount(1) },
            { "gc_gen2_collections", GC.CollectionCount(2) }
        };

        if (allocated >= _threshold)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded($"Memory usage ({allocated / (1024 * 1024)}MB) exceeds threshold", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Memory usage is healthy", data));
    }
}

/// <summary>
/// Session cleanup health check - verifies cleanup is working
/// </summary>
public class SessionHealthCheck : IHealthCheck
{
    private readonly DataForemanDbContext _context;

    public SessionHealthCheck(DataForemanDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var totalSessions = await _context.Sessions.CountAsync(cancellationToken);
            var activeSessions = await _context.Sessions
                .CountAsync(s => s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow, cancellationToken);
            var expiredSessions = await _context.Sessions
                .CountAsync(s => s.ExpiresAt <= DateTime.UtcNow && s.RevokedAt == null, cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "total_sessions", totalSessions },
                { "active_sessions", activeSessions },
                { "expired_pending_cleanup", expiredSessions }
            };

            if (expiredSessions > 100)
            {
                return HealthCheckResult.Degraded("Many expired sessions pending cleanup", data: data);
            }

            return HealthCheckResult.Healthy("Session management is healthy", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Session health check failed", ex);
        }
    }
}
