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
    
    private bool _isRunning;
    private DateTime _startTime;
    private long _totalPolls;
    private double _totalPollTimeMs;
    private Timer? _statusTimer;

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
        _statusTimer = new Timer(async _ =>
        {
            try { await PublishEngineStatusAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Error in engine status publish timer"); }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        _logger.LogInformation("Poll engine started with {ConnectionCount} connections", _pollers.Count);
    }

    /// <summary>
    /// Stops the polling engine.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _statusTimer?.Dispose();
        _statusTimer = null;

        foreach (var poller in _pollers.Values)
        {
            await poller.StopAsync();
        }
        _pollers.Clear();

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
                OnTagValuesReceived,
                OnConnectionStatusChanged);

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

    /// <summary>
    /// Writes a value to a tag on the specified connection.
    /// Used by state machine actions to set tag values on external devices.
    /// </summary>
    public async Task WriteTagAsync(string connectionId, TagConfig tag, object value)
    {
        if (_pollers.TryGetValue(connectionId, out var poller))
        {
            await poller.WriteTagAsync(tag, value);
        }
        else
        {
            _logger.LogWarning("Cannot write tag {TagName}: connection {ConnectionId} not active",
                tag.Name, connectionId);
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

    private async void OnTagValuesReceived(string connectionId, Dictionary<string, TagValue> values, double pollTimeMs)
    {
        try
        {
            var connection = _configService.GetConnection(connectionId);
            if (connection == null) return;

            Interlocked.Increment(ref _totalPolls);
            // Atomic add for double using compare-exchange loop
            double initial, computed;
            do
            {
                initial = _totalPollTimeMs;
                computed = initial + pollTimeMs;
            }
            while (Interlocked.CompareExchange(ref _totalPollTimeMs, computed, initial) != initial);

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

    private async void OnConnectionStatusChanged(string connectionId, ConnectionState state, string? errorMessage)
    {
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
/// Manages polling for a single connection.
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
    
    private bool _isRunning;

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
                
                var timer = new Timer(
                    async _ => await PollGroupAsync(pollRateMs, tags),
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
        _isRunning = false;

        foreach (var timer in _pollTimers.Values)
        {
            await timer.DisposeAsync();
        }
        _pollTimers.Clear();
        _pollGroups.Clear();

        try
        {
            await _driver.DisconnectAsync();
            _onStatusChanged(_connection.Id, ConnectionState.Disconnected, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting driver: {ConnectionName}", _connection.Name);
        }
    }

    private async Task PollGroupAsync(int pollRateMs, List<TagConfig> tags)
    {
        if (!_isRunning || !_driver.IsConnected) return;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var values = await _driver.ReadTagsAsync(tags);
            stopwatch.Stop();

            if (values.Count > 0)
            {
                _onValuesReceived(_connection.Id, values, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling tags for connection {ConnectionName}, group {PollRateMs}ms",
                _connection.Name, pollRateMs);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _driver.DisposeAsync();
    }

    /// <summary>
    /// Writes a value to a tag via the underlying driver.
    /// </summary>
    public async Task WriteTagAsync(TagConfig tag, object value)
    {
        if (!_driver.IsConnected)
        {
            _logger.LogWarning("Cannot write tag {TagName}: driver for {Connection} is not connected",
                tag.Name, _connection.Name);
            return;
        }
        await _driver.WriteTagAsync(tag, value);
    }
}
