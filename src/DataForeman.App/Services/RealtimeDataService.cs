using System.Collections.Concurrent;
using DataForeman.Shared.Models;

namespace DataForeman.App.Services;

/// <summary>
/// Service that caches real-time tag values and provides them to the UI
/// </summary>
public class RealtimeDataService : IDisposable
{
    private readonly MqttService _mqtt;
    private readonly ConcurrentDictionary<string, TagValueMessage> _tagValues = new();
    private readonly ConcurrentDictionary<string, List<TagDataPoint>> _tagHistory = new();
    private EngineStatusMessage? _engineStatus;

    public event Action? OnDataChanged;
    public event Action<EngineStatusMessage>? OnEngineStatusChanged;

    public RealtimeDataService(MqttService mqtt)
    {
        _mqtt = mqtt;
        _mqtt.OnTagValue += HandleTagValue;
        _mqtt.OnHistoryData += HandleHistoryData;
        _mqtt.OnEngineStatus += HandleEngineStatus;
    }

    /// <summary>
    /// Get the current value of a tag
    /// </summary>
    public TagValueMessage? GetTagValue(string connectionId, string tagId)
    {
        var key = $"{connectionId}/{tagId}";
        return _tagValues.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Get all current tag values
    /// </summary>
    public IEnumerable<TagValueMessage> GetAllTagValues()
        => _tagValues.Values;

    /// <summary>
    /// Get tag values for a specific connection
    /// </summary>
    public IEnumerable<TagValueMessage> GetTagValues(string connectionId)
        => _tagValues.Values.Where(v => v.ConnectionId == connectionId);

    /// <summary>
    /// Get cached history for a tag (recent points received from subscription)
    /// </summary>
    public IReadOnlyList<TagDataPoint> GetTagHistory(string connectionId, string tagId, int maxPoints = 500)
    {
        var key = $"{connectionId}/{tagId}";
        if (_tagHistory.TryGetValue(key, out var history))
        {
            return history.TakeLast(maxPoints).ToList();
        }
        return Array.Empty<TagDataPoint>();
    }

    /// <summary>
    /// Request history from the engine for a specific time range
    /// </summary>
    public Task RequestHistoryAsync(string connectionId, string tagId, DateTime start, DateTime end,
        int maxPoints = 1000, AggregationType aggregation = AggregationType.None)
        => _mqtt.RequestHistoryAsync(connectionId, tagId, start, end, maxPoints, aggregation);

    /// <summary>
    /// Get the latest engine status
    /// </summary>
    public EngineStatusMessage? EngineStatus => _engineStatus;

    /// <summary>
    /// Check if the engine is connected and running
    /// </summary>
    public bool IsEngineRunning => _engineStatus?.Running ?? false;

    private void HandleTagValue(TagValueMessage msg)
    {
        var key = $"{msg.ConnectionId}/{msg.TagId}";
        _tagValues[key] = msg;

        // Also add to rolling history
        var history = _tagHistory.GetOrAdd(key, _ => new List<TagDataPoint>());
        lock (history)
        {
            history.Add(new TagDataPoint
            {
                Timestamp = msg.Timestamp,
                Value = msg.Value,
                Quality = msg.Quality
            });

            // Keep only last 1000 points in memory
            if (history.Count > 1000)
            {
                history.RemoveRange(0, history.Count - 1000);
            }
        }

        OnDataChanged?.Invoke();
    }

    private void HandleHistoryData(TagHistoryMessage msg)
    {
        var key = $"{msg.ConnectionId}/{msg.TagId}";
        var history = _tagHistory.GetOrAdd(key, _ => new List<TagDataPoint>());

        lock (history)
        {
            // Insert historical points (they should be older than real-time)
            foreach (var point in msg.Points)
            {
                // Find insertion point to keep sorted
                var index = history.FindIndex(p => p.Timestamp > point.Timestamp);
                if (index < 0)
                    history.Add(point);
                else
                    history.Insert(index, point);
            }

            // Deduplicate by timestamp
            var deduped = history.DistinctBy(p => p.Timestamp).OrderBy(p => p.Timestamp).ToList();
            history.Clear();
            history.AddRange(deduped);

            // Keep max 5000 points
            if (history.Count > 5000)
            {
                history.RemoveRange(0, history.Count - 5000);
            }
        }

        OnDataChanged?.Invoke();
    }

    private void HandleEngineStatus(EngineStatusMessage msg)
    {
        _engineStatus = msg;
        OnEngineStatusChanged?.Invoke(msg);
    }

    public void Dispose()
    {
        _mqtt.OnTagValue -= HandleTagValue;
        _mqtt.OnHistoryData -= HandleHistoryData;
        _mqtt.OnEngineStatus -= HandleEngineStatus;
    }
}
