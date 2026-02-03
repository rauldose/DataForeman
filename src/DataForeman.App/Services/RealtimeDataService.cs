using System.Collections.Concurrent;
using DataForeman.Shared.Mqtt;

namespace DataForeman.App.Services;

/// <summary>
/// Service for caching and providing real-time tag values to the UI.
/// </summary>
public class RealtimeDataService : IDisposable
{
    private readonly ILogger<RealtimeDataService> _logger;
    private readonly MqttService _mqttService;
    private readonly ConcurrentDictionary<string, TagValueCache> _tagValues = new();
    private readonly ConcurrentDictionary<string, ConnectionStatusMessage> _connectionStatuses = new();
    private EngineStatusMessage? _engineStatus;
    
    public event Action? OnDataChanged;
    public event Action<string>? OnTagValueChanged;
    public event Action<string>? OnConnectionStatusChanged;
    public event Action? OnEngineStatusChanged;

    public RealtimeDataService(MqttService mqttService, ILogger<RealtimeDataService> logger)
    {
        _mqttService = mqttService;
        _logger = logger;

        // Subscribe to MQTT events
        _mqttService.OnTagValueReceived += HandleTagValue;
        _mqttService.OnBulkTagValuesReceived += HandleBulkTagValues;
        _mqttService.OnConnectionStatusReceived += HandleConnectionStatus;
        _mqttService.OnEngineStatusReceived += HandleEngineStatus;
    }

    /// <summary>
    /// Gets the current value for a tag.
    /// </summary>
    public TagValueCache? GetTagValue(string tagId)
    {
        _tagValues.TryGetValue(tagId, out var value);
        return value;
    }

    /// <summary>
    /// Gets all current tag values.
    /// </summary>
    public IReadOnlyDictionary<string, TagValueCache> GetAllTagValues() 
        => _tagValues;

    /// <summary>
    /// Gets tag values for a specific connection.
    /// </summary>
    public IEnumerable<TagValueCache> GetTagValuesByConnection(string connectionId)
    {
        return _tagValues.Values.Where(v => v.ConnectionId == connectionId);
    }

    /// <summary>
    /// Gets the status for a connection.
    /// </summary>
    public ConnectionStatusMessage? GetConnectionStatus(string connectionId)
    {
        _connectionStatuses.TryGetValue(connectionId, out var status);
        return status;
    }

    /// <summary>
    /// Gets all connection statuses.
    /// </summary>
    public IReadOnlyDictionary<string, ConnectionStatusMessage> GetAllConnectionStatuses() 
        => _connectionStatuses;

    /// <summary>
    /// Gets the engine status.
    /// </summary>
    public EngineStatusMessage? GetEngineStatus() => _engineStatus;

    /// <summary>
    /// Gets historical values for a tag (from memory cache).
    /// </summary>
    public IReadOnlyList<(DateTime Timestamp, object? Value)> GetTagHistory(string tagId, int maxPoints = 100)
    {
        if (_tagValues.TryGetValue(tagId, out var cache))
        {
            return cache.History.TakeLast(maxPoints).ToList();
        }
        return Array.Empty<(DateTime, object?)>();
    }

    private void HandleTagValue(TagValueMessage message)
    {
        try
        {
            var key = message.TagId;
            var cache = _tagValues.GetOrAdd(key, _ => new TagValueCache
            {
                TagId = message.TagId,
                TagName = message.TagName,
                ConnectionId = message.ConnectionId,
                DataType = message.DataType
            });

            cache.Value = message.Value;
            cache.Quality = message.Quality;
            cache.Timestamp = message.Timestamp;
            cache.AddToHistory(message.Timestamp, message.Value);

            OnTagValueChanged?.Invoke(key);
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tag value for {TagId}", message.TagId);
        }
    }

    private void HandleBulkTagValues(BulkTagValueMessage message)
    {
        try
        {
            foreach (var tagValue in message.Tags)
            {
                var key = tagValue.TagId;
                var cache = _tagValues.GetOrAdd(key, _ => new TagValueCache
                {
                    TagId = tagValue.TagId,
                    TagName = tagValue.TagName,
                    ConnectionId = tagValue.ConnectionId,
                    DataType = tagValue.DataType
                });

                cache.Value = tagValue.Value;
                cache.Quality = tagValue.Quality;
                cache.Timestamp = tagValue.Timestamp;
                cache.AddToHistory(tagValue.Timestamp, tagValue.Value);
            }

            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling bulk tag values for connection {ConnectionId}", message.ConnectionId);
        }
    }

    private void HandleConnectionStatus(ConnectionStatusMessage message)
    {
        try
        {
            _connectionStatuses[message.ConnectionId] = message;
            OnConnectionStatusChanged?.Invoke(message.ConnectionId);
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection status for {ConnectionId}", message.ConnectionId);
        }
    }

    private void HandleEngineStatus(EngineStatusMessage message)
    {
        try
        {
            _engineStatus = message;
            OnEngineStatusChanged?.Invoke();
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling engine status");
        }
    }

    public void Dispose()
    {
        _mqttService.OnTagValueReceived -= HandleTagValue;
        _mqttService.OnBulkTagValuesReceived -= HandleBulkTagValues;
        _mqttService.OnConnectionStatusReceived -= HandleConnectionStatus;
        _mqttService.OnEngineStatusReceived -= HandleEngineStatus;
    }
}

/// <summary>
/// Cached tag value with history.
/// </summary>
public class TagValueCache
{
    private readonly List<(DateTime Timestamp, object? Value)> _history = new();
    private readonly object _lock = new();
    private const int MaxHistoryPoints = 500;

    public string TagId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string DataType { get; set; } = "Float";
    public object? Value { get; set; }
    public int Quality { get; set; }
    public DateTime Timestamp { get; set; }

    public IReadOnlyList<(DateTime Timestamp, object? Value)> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }
    }

    public void AddToHistory(DateTime timestamp, object? value)
    {
        lock (_lock)
        {
            _history.Add((timestamp, value));
            if (_history.Count > MaxHistoryPoints)
            {
                _history.RemoveRange(0, _history.Count - MaxHistoryPoints);
            }
        }
    }

    public string FormattedValue => Value switch
    {
        null => "N/A",
        bool b => b ? "True" : "False",
        double d => d.ToString("F2"),
        float f => f.ToString("F2"),
        _ => Value.ToString() ?? "N/A"
    };

    public string QualityText => Quality == 0 ? "Good" : "Bad";
    public bool IsGoodQuality => Quality == 0;
}
