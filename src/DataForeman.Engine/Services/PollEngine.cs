using System.Collections.Concurrent;
using System.Diagnostics;
using DataForeman.Engine.Drivers;
using DataForeman.Shared.Models;
using DataForeman.Shared.Mqtt;

namespace DataForeman.Engine.Services;

/// <summary>
/// High-speed polling engine with sub-50ms capability.
/// Uses timer-based parallel tag polling per connection.
/// </summary>
public class PollEngine : IAsyncDisposable
{
    private readonly ILogger<PollEngine> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConfigService _configService;
    private readonly MqttPublisher _mqttPublisher;
    private readonly HistoryStore _historyStore;

    private readonly ConcurrentDictionary<string, ConnectionPoller> _pollers = new();
    private readonly ConcurrentDictionary<string, TagValue> _currentValues = new();

    private volatile bool _isRunning;
    private volatile bool _isStopping;
    private DateTime _startTime;
    private long _totalPolls;
    private double _totalPollTimeMs;
    private Timer? _statusTimer;
    private readonly object _statusTimerLock = new();

    public bool IsRunning => _isRunning;
    public IReadOnlyDictionary<string, TagValue> CurrentValues => _currentValues;

    public PollEngine(
        ILogger<PollEngine> logger,
        IServiceProvider serviceProvider,
        ConfigService configService,
        MqttPublisher mqttPublisher,
        HistoryStore historyStore)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configService = configService;
        _mqttPublisher = mqttPublisher;
        _historyStore = historyStore;
    }

    /// <summary>
    /// Starts the polling engine.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning) return;

        _isRunning = true;
        _startTime = DateTime.UtcNow;
        _totalPolls = 0;
        _totalPollTimeMs = 0;

        // Start pollers for each enabled connection
        foreach (var connection in _configService.Connections.Where(c => c.Enabled))
        {
            await StartConnectionPollerAsync(connection);
        }

        // Start status reporting timer (every 5 seconds)
        _statusTimer = new Timer(async _ => await PublishEngineStatusAsync(), null, 
            TimeSpan.Zero, TimeSpan.FromSeconds(5));

        _logger.LogInformation("Poll engine started with {ConnectionCount} connections", _pollers.Count);
    }

    /// <summary>
    /// Stops the polling engine.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning || _isStopping) return;

        _isStopping = true;
        _isRunning = false;

        // Safely dispose status timer
        lock (_statusTimerLock)
        {
            _statusTimer?.Dispose();
            _statusTimer = null;
        }

        // Stop all pollers concurrently
        var stopTasks = _pollers.Values.Select(p => p.StopAsync()).ToList();
        try
        {
            await Task.WhenAll(stopTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping one or more pollers");
        }
        _pollers.Clear();

        _isStopping = false;
        _logger.LogInformation("Poll engine stopped");
    }

    /// <summary>
    /// Reloads configuration and restarts affected pollers.
    /// </summary>
    public async Task ReloadConfigurationAsync()
    {
        _logger.LogInformation("Reloading poll engine configuration");

        // Stop pollers for connections that no longer exist or are disabled
        var currentConnectionIds = _configService.Connections.Where(c => c.Enabled).Select(c => c.Id).ToHashSet();
        var pollersToRemove = _pollers.Keys.Where(id => !currentConnectionIds.Contains(id)).ToList();

        foreach (var id in pollersToRemove)
        {
            if (_pollers.TryRemove(id, out var poller))
            {
                await poller.StopAsync();
                _logger.LogInformation("Stopped poller for removed/disabled connection: {ConnectionId}", id);
            }
        }

        // Start or restart pollers for enabled connections
        foreach (var connection in _configService.Connections.Where(c => c.Enabled))
        {
            if (_pollers.ContainsKey(connection.Id))
            {
                // Restart existing poller to pick up tag changes
                if (_pollers.TryRemove(connection.Id, out var existingPoller))
                {
                    await existingPoller.StopAsync();
                }
            }
            await StartConnectionPollerAsync(connection);
        }
    }

    private async Task StartConnectionPollerAsync(ConnectionConfig connection)
    {
        try
        {
            var driver = CreateDriver(connection.Type);
            if (driver == null)
            {
                _logger.LogWarning("No driver available for connection type: {Type}", connection.Type);
                return;
            }

            var poller = new ConnectionPoller(
                connection,
                driver,
                _logger,
                (connId, values, pollTime) =>
                {
                    SafeFireAndForget(OnTagValuesReceivedAsync(connId, values, pollTime), "OnTagValuesReceived");
                },
                (connId, state, errorMsg) =>
                {
                    SafeFireAndForget(OnConnectionStatusChangedAsync(connId, state, errorMsg), "OnConnectionStatusChanged");
                });

            _pollers[connection.Id] = poller;
            await poller.StartAsync();

            _logger.LogInformation("Started poller for connection: {ConnectionName} ({Type})",
                connection.Name, connection.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting poller for connection: {ConnectionName}", connection.Name);
        }
    }

    private IDriver? CreateDriver(string driverType)
    {
        return driverType.ToLowerInvariant() switch
        {
            "simulator" => new SimulatorDriver(_serviceProvider.GetRequiredService<ILogger<SimulatorDriver>>()),
            // Add more drivers here as needed
            _ => null
        };
    }

    /// <summary>
    /// Handles tag values received from a connection poller.
    /// This method is invoked as a fire-and-forget but exceptions are observed and logged.
    /// </summary>
    private async Task OnTagValuesReceivedAsync(string connectionId, Dictionary<string, TagValue> values, double pollTimeMs)
    {
        if (_isStopping) return;

        try
        {
            var connection = _configService.GetConnection(connectionId);
            if (connection == null) return;

            Interlocked.Increment(ref _totalPolls);
            Interlocked.Exchange(ref _totalPollTimeMs, _totalPollTimeMs + pollTimeMs);

            // Update current values cache
            foreach (var kvp in values)
            {
                _currentValues[kvp.Key] = kvp.Value;
            }

            // Publish to MQTT
            var bulkMessage = new BulkTagValueMessage
            {
                ConnectionId = connectionId,
                Timestamp = DateTime.UtcNow,
                Tags = values.Select(kvp =>
                {
                    var tag = connection.Tags.FirstOrDefault(t => t.Id == kvp.Key);
                    return new TagValueMessage
                    {
                        ConnectionId = connectionId,
                        TagId = kvp.Key,
                        TagName = tag?.Name ?? kvp.Key,
                        Value = kvp.Value.Value,
                        DataType = tag?.DataType ?? "Float",
                        Quality = kvp.Value.Quality,
                        Timestamp = kvp.Value.Timestamp
                    };
                }).ToList()
            };

            await _mqttPublisher.PublishBulkTagValuesAsync(bulkMessage);

            // Store in history
            foreach (var kvp in values)
            {
                await _historyStore.StoreValueAsync(connectionId, kvp.Key, kvp.Value.Value, kvp.Value.Quality, kvp.Value.Timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tag values for connection: {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Handles connection status changes from a connection poller.
    /// This method is invoked as a fire-and-forget but exceptions are observed and logged.
    /// </summary>
    private async Task OnConnectionStatusChangedAsync(string connectionId, ConnectionState state, string? errorMessage)
    {
        if (_isStopping) return;

        try
        {
            var connection = _configService.GetConnection(connectionId);
            var statusMessage = new ConnectionStatusMessage
            {
                ConnectionId = connectionId,
                ConnectionName = connection?.Name ?? connectionId,
                State = state,
                ErrorMessage = errorMessage,
                ActiveTagCount = connection?.Tags.Count(t => t.Enabled) ?? 0,
                Timestamp = DateTime.UtcNow
            };

            await _mqttPublisher.PublishConnectionStatusAsync(statusMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing connection status for: {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Safe fire-and-forget wrapper that observes exceptions.
    /// </summary>
    private void SafeFireAndForget(Task task, string operationName)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "Unobserved exception in {OperationName}", operationName);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task PublishEngineStatusAsync()
    {
        try
        {
            var statusMessage = new EngineStatusMessage
            {
                IsRunning = _isRunning,
                ActiveConnections = _pollers.Count(p => p.Value.IsConnected),
                ActiveTags = _currentValues.Count,
                TotalPolls = _totalPolls,
                AveragePollTimeMs = _totalPolls > 0 ? _totalPollTimeMs / _totalPolls : 0,
                StartTime = _startTime,
                Timestamp = DateTime.UtcNow
            };

            await _mqttPublisher.PublishEngineStatusAsync(statusMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing engine status");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}

/// <summary>
/// Manages polling for a single connection with circuit breaker and backpressure support.
/// </summary>
internal class ConnectionPoller : IAsyncDisposable
{
    private readonly ConnectionConfig _connection;
    private readonly IDriver _driver;
    private readonly ILogger _logger;
    private readonly Action<string, Dictionary<string, TagValue>, double> _onValuesReceived;
    private readonly Action<string, ConnectionState, string?> _onStatusChanged;

    private readonly ConcurrentDictionary<int, Timer> _pollTimers = new();
    private readonly ConcurrentDictionary<int, List<TagConfig>> _pollGroups = new();
    private readonly ConcurrentDictionary<int, int> _pollInProgress = new(); // Backpressure tracking

    // Circuit breaker state
    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int CircuitBreakerThreshold = 5;
    private const int CircuitBreakerResetSeconds = 30;

    private volatile bool _isRunning;
    private volatile bool _isStopping;

    public bool IsConnected => _driver.IsConnected;

    public ConnectionPoller(
        ConnectionConfig connection,
        IDriver driver,
        ILogger logger,
        Action<string, Dictionary<string, TagValue>, double> onValuesReceived,
        Action<string, ConnectionState, string?> onStatusChanged)
    {
        _connection = connection;
        _driver = driver;
        _logger = logger;
        _onValuesReceived = onValuesReceived;
        _onStatusChanged = onStatusChanged;
    }

    public async Task StartAsync()
    {
        try
        {
            _onStatusChanged(_connection.Id, ConnectionState.Connecting, null);
            await _driver.ConnectAsync(_connection);
            _isRunning = true;
            _onStatusChanged(_connection.Id, ConnectionState.Connected, null);

            // Group tags by poll rate
            var enabledTags = _connection.Tags.Where(t => t.Enabled).ToList();
            var groups = enabledTags.GroupBy(t => t.PollRateMs).ToDictionary(g => g.Key, g => g.ToList());

            // Create a timer for each poll rate group
            foreach (var group in groups)
            {
                var pollRateMs = group.Key;
                var tags = group.Value;

                _pollGroups[pollRateMs] = tags;
                _pollInProgress[pollRateMs] = 0;

                var timer = new Timer(
                    _ => PollGroupWithBackpressure(pollRateMs, tags),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(pollRateMs));

                _pollTimers[pollRateMs] = timer;
            }

            _logger.LogInformation("Connection {Name}: Started {GroupCount} poll groups for {TagCount} tags",
                _connection.Name, groups.Count, enabledTags.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting connection poller: {ConnectionName}", _connection.Name);
            _onStatusChanged(_connection.Id, ConnectionState.Error, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        if (_isStopping) return;
        _isStopping = true;
        _isRunning = false;

        // Stop all timers first to prevent new callbacks
        var timers = _pollTimers.Values.ToList();
        _pollTimers.Clear();

        foreach (var timer in timers)
        {
            try
            {
                await timer.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing timer for connection {ConnectionName}", _connection.Name);
            }
        }
        _pollGroups.Clear();
        _pollInProgress.Clear();

        try
        {
            await _driver.DisconnectAsync();
            _onStatusChanged(_connection.Id, ConnectionState.Disconnected, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting driver: {ConnectionName}", _connection.Name);
        }

        _isStopping = false;
    }

    /// <summary>
    /// Timer callback with backpressure - skips poll if previous is still running.
    /// </summary>
    private void PollGroupWithBackpressure(int pollRateMs, List<TagConfig> tags)
    {
        if (!_isRunning || _isStopping) return;

        // Backpressure: skip if previous poll is still in progress
        if (Interlocked.CompareExchange(ref _pollInProgress.GetOrAdd(pollRateMs, 0), 1, 0) != 0)
        {
            _logger.LogDebug("Skipping poll for {ConnectionName} group {PollRateMs}ms - previous poll still in progress",
                _connection.Name, pollRateMs);
            return;
        }

        // Fire and forget with exception handling
        _ = PollGroupAsync(pollRateMs, tags).ContinueWith(t =>
        {
            // Release backpressure lock
            _pollInProgress[pollRateMs] = 0;

            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "Unobserved exception in poll for {ConnectionName}", _connection.Name);
            }
        });
    }

    private async Task PollGroupAsync(int pollRateMs, List<TagConfig> tags)
    {
        if (!_isRunning || _isStopping) return;

        // Circuit breaker check
        if (DateTime.UtcNow < _circuitOpenUntil)
        {
            _logger.LogDebug("Circuit breaker open for {ConnectionName}, skipping poll", _connection.Name);
            return;
        }

        if (!_driver.IsConnected)
        {
            RecordFailure("Driver not connected");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var values = await _driver.ReadTagsAsync(tags);
            stopwatch.Stop();

            if (values.Count > 0)
            {
                _onValuesReceived(_connection.Id, values, stopwatch.Elapsed.TotalMilliseconds);
            }

            // Reset circuit breaker on success
            ResetCircuitBreaker();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordFailure(ex.Message);
            _logger.LogError(ex, "Error polling tags for connection {ConnectionName}, group {PollRateMs}ms",
                _connection.Name, pollRateMs);
        }
    }

    private void RecordFailure(string reason)
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= CircuitBreakerThreshold)
        {
            _circuitOpenUntil = DateTime.UtcNow.AddSeconds(CircuitBreakerResetSeconds);
            _logger.LogWarning(
                "Circuit breaker opened for {ConnectionName} after {Failures} consecutive failures. Will retry in {Seconds}s. Reason: {Reason}",
                _connection.Name, failures, CircuitBreakerResetSeconds, reason);
            _onStatusChanged(_connection.Id, ConnectionState.Error, $"Circuit breaker opened: {reason}");
        }
    }

    private void ResetCircuitBreaker()
    {
        if (_consecutiveFailures > 0)
        {
            var wasOpen = _circuitOpenUntil > DateTime.MinValue;
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            _circuitOpenUntil = DateTime.MinValue;

            if (wasOpen)
            {
                _logger.LogInformation("Circuit breaker reset for {ConnectionName}", _connection.Name);
                _onStatusChanged(_connection.Id, ConnectionState.Connected, null);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _driver.DisposeAsync();
    }
}
