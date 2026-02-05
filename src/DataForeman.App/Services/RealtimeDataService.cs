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
    private volatile bool _isDisposed;

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

    /// <summary>
    /// Gets historical values for a tag within a time window for charting (from memory cache).
    /// </summary>
    public IReadOnlyList<TagHistoryPoint> GetTagHistoryForChart(string tagId, int windowSeconds)
    {
        if (_tagValues.TryGetValue(tagId, out var cache))
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-windowSeconds);
            return cache.History
                .Where(h => h.Timestamp >= cutoff)
                .Select(h => new TagHistoryPoint
                {
                    Timestamp = h.Timestamp,
                    NumericValue = ConvertToDouble(h.Value)
                })
                .ToList();
        }
        return Array.Empty<TagHistoryPoint>();
    }

    private static double? ConvertToDouble(object? value)
    {
        return value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal dec => (double)dec,
            bool b => b ? 1.0 : 0.0,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private void HandleTagValue(TagValueMessage message)
    {
        if (_isDisposed) return;

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

            SafeInvoke(() => OnTagValueChanged?.Invoke(key), "OnTagValueChanged");
            SafeInvoke(() => OnDataChanged?.Invoke(), "OnDataChanged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tag value for {TagId}", message.TagId);
        }
    }

    private void HandleBulkTagValues(BulkTagValueMessage message)
    {
        if (_isDisposed) return;

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

            SafeInvoke(() => OnDataChanged?.Invoke(), "OnDataChanged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling bulk tag values for connection {ConnectionId}", message.ConnectionId);
        }
    }

    private void HandleConnectionStatus(ConnectionStatusMessage message)
    {
        if (_isDisposed) return;

        try
        {
            _connectionStatuses[message.ConnectionId] = message;
            SafeInvoke(() => OnConnectionStatusChanged?.Invoke(message.ConnectionId), "OnConnectionStatusChanged");
            SafeInvoke(() => OnDataChanged?.Invoke(), "OnDataChanged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection status for {ConnectionId}", message.ConnectionId);
        }
    }

    private void HandleEngineStatus(EngineStatusMessage message)
    {
        if (_isDisposed) return;

        try
        {
            _engineStatus = message;
            SafeInvoke(() => OnEngineStatusChanged?.Invoke(), "OnEngineStatusChanged");
            SafeInvoke(() => OnDataChanged?.Invoke(), "OnDataChanged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling engine status");
        }
    }

    private void SafeInvoke(Action action, string handlerName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {HandlerName} event handler", handlerName);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
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

    public double? NumericValue => Value switch
    {
        null => null,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal dec => (double)dec,
        bool b => b ? 1.0 : 0.0,
        string s when double.TryParse(s, out var parsed) => parsed,
        _ => null
    };
}

/// <summary>
/// Historical data point for charts.
/// </summary>
public class TagHistoryPoint
{
    public DateTime Timestamp { get; set; }
    public double? NumericValue { get; set; }
}
