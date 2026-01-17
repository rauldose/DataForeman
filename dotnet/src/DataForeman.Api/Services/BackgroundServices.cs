using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;

namespace DataForeman.Api.Services;

/// <summary>
/// Background service for cleaning up expired sessions
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retentionPeriod;

    public SessionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        
        // Default: run every hour, keep revoked sessions for 7 days
        _interval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("SessionCleanup:IntervalMinutes", 60));
        _retentionPeriod = TimeSpan.FromDays(
            configuration.GetValue<int>("SessionCleanup:RetentionDays", 7));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Session cleanup service started. Interval: {Interval}, Retention: {Retention}",
            _interval, _retentionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Session cleanup service stopped");
    }

    private async Task CleanupSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataForemanDbContext>();

        var cutoffDate = DateTime.UtcNow - _retentionPeriod;

        // Mark expired sessions as revoked
        var expiredCount = await context.Sessions
            .Where(s => s.RevokedAt == null && s.ExpiresAt <= DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Revoked {Count} expired sessions", expiredCount);
        }

        // Delete old revoked sessions (past retention period)
        var deletedCount = await context.Sessions
            .Where(s => s.RevokedAt != null && s.RevokedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} old revoked sessions", deletedCount);
        }
    }
}

/// <summary>
/// Background service for periodic cache refresh
/// </summary>
public class CacheRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CacheRefreshService> _logger;
    private readonly TimeSpan _interval;

    public CacheRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<CacheRefreshService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        
        // Default: refresh cache every 5 minutes
        _interval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("CacheRefresh:IntervalMinutes", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache refresh service started. Interval: {Interval}", _interval);

        // Wait a bit before first refresh to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshCacheAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache refresh");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Cache refresh service stopped");
    }

    private async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataForemanDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        // Preload commonly accessed data into cache
        await cacheService.GetPollGroupsAsync(async () =>
            await context.PollGroups.Where(p => p.IsActive).ToListAsync(cancellationToken));

        await cacheService.GetUnitsOfMeasureAsync(async () =>
            await context.UnitsOfMeasure.ToListAsync(cancellationToken));

        _logger.LogDebug("Cache refreshed successfully");
    }
}
