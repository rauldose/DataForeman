using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Messages;
using DataForeman.Shared.Models;
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
    private readonly ConcurrentDictionary<string, List<FlowExecutionMessage>> _flowExecutionCache = new();
    private readonly ConcurrentDictionary<string, InternalTagValueCache> _internalTagValues = new();
    private readonly ConcurrentDictionary<string, MachineRuntimeInfo> _machineStates = new();
    private readonly ConcurrentDictionary<string, List<FlowRunSummaryMessage>> _flowRunHistory = new();
    private readonly ConcurrentDictionary<string, FlowDeploymentStatusMessage> _flowDeployStatuses = new();
    private EngineStatusMessage? _engineStatus;
    
    public event Action? OnDataChanged;
    public event Action<string>? OnTagValueChanged;
    public event Action<string>? OnConnectionStatusChanged;
    public event Action? OnEngineStatusChanged;
    public event Action<FlowExecutionMessage>? OnFlowExecutionReceived;
    public event Action<MachineRuntimeInfo>? OnStateMachineStateChanged;
    public event Action<FlowRunSummaryMessage>? OnFlowRunSummaryReceived;
    public event Action<string>? OnFlowDeployStatusChanged;

    public RealtimeDataService(MqttService mqttService, ILogger<RealtimeDataService> logger)
    {
        _mqttService = mqttService;
        _logger = logger;

        // Subscribe to MQTT events
        _mqttService.OnTagValueReceived += HandleTagValue;
        _mqttService.OnBulkTagValuesReceived += HandleBulkTagValues;
        _mqttService.OnConnectionStatusReceived += HandleConnectionStatus;
        _mqttService.OnEngineStatusReceived += HandleEngineStatus;
        _mqttService.OnFlowExecutionReceived += HandleFlowExecution;
        _mqttService.OnStateMachineStateReceived += HandleStateMachineState;
        _mqttService.OnFlowRunSummaryReceived += HandleFlowRunSummary;
        _mqttService.OnFlowDeployStatusReceived += HandleFlowDeployStatus;
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

    #region Internal Tags

    /// <summary>
    /// Gets all internal tag values.
    /// </summary>
    public IReadOnlyDictionary<string, InternalTagValueCache> GetInternalTagValues()
        => _internalTagValues;

    /// <summary>
    /// Sets an internal tag value.
    /// </summary>
    public void SetInternalTag(string key, object? value)
    {
        var cache = _internalTagValues.GetOrAdd(key, k => new InternalTagValueCache { Key = k });
        cache.Value = value;
        cache.TimestampUtc = DateTime.UtcNow;
        
        _logger.LogDebug("Set internal tag '{Key}' = {Value}", key, value);
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Deletes an internal tag.
    /// </summary>
    public void DeleteInternalTag(string key)
    {
        if (_internalTagValues.TryRemove(key, out _))
        {
            _logger.LogDebug("Deleted internal tag '{Key}'", key);
            OnDataChanged?.Invoke();
        }
    }

    /// <summary>
    /// Gets an internal tag value.
    /// </summary>
    public InternalTagValueCache? GetInternalTag(string key)
    {
        _internalTagValues.TryGetValue(key, out var value);
        return value;
    }

    #endregion

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
    /// Gets flow execution traces for a specific flow.
    /// </summary>
    public IReadOnlyList<FlowExecutionMessage> GetFlowExecutionTraces(string flowId, int maxTraces = 100)
    {
        if (_flowExecutionCache.TryGetValue(flowId, out var traces))
        {
            lock (traces)
            {
                return traces.TakeLast(maxTraces).ToList();
            }
        }
        return Array.Empty<FlowExecutionMessage>();
    }

    /// <summary>
    /// Clears flow execution traces for a specific flow.
    /// </summary>
    public void ClearFlowExecutionTraces(string flowId)
    {
        if (_flowExecutionCache.TryGetValue(flowId, out var traces))
        {
            lock (traces)
            {
                traces.Clear();
            }
        }
    }

    /// <summary>
    /// Gets the cached runtime state for a state machine.
    /// </summary>
    public MachineRuntimeInfo? GetMachineState(string machineId)
    {
        _machineStates.TryGetValue(machineId, out var info);
        return info;
    }

    /// <summary>
    /// Gets all cached state machine runtime states.
    /// </summary>
    public IReadOnlyDictionary<string, MachineRuntimeInfo> GetAllMachineStates()
        => _machineStates;

    /// <summary>
    /// Gets recent flow run summaries for a specific flow.
    /// </summary>
    public IReadOnlyList<FlowRunSummaryMessage> GetFlowRunHistory(string flowId, int maxEntries = 50)
    {
        if (_flowRunHistory.TryGetValue(flowId, out var runs))
        {
            lock (runs)
            {
                return runs.TakeLast(maxEntries).ToList();
            }
        }
        return Array.Empty<FlowRunSummaryMessage>();
    }

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
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetDouble(),
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(je.GetString(), out var p) => p,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.True => 1.0,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.False => 0.0,
            _ => null
        };
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
                
                // Fire individual tag change event for each tag
                OnTagValueChanged?.Invoke(key);
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

    private void HandleFlowExecution(FlowExecutionMessage message)
    {
        try
        {
            var traces = _flowExecutionCache.GetOrAdd(message.FlowId, _ => new List<FlowExecutionMessage>());
            lock (traces)
            {
                traces.Add(message);
                // Keep only last 500 traces per flow
                if (traces.Count > 500)
                {
                    traces.RemoveRange(0, traces.Count - 500);
                }
            }
            OnFlowExecutionReceived?.Invoke(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling flow execution message for flow {FlowId}", message.FlowId);
        }
    }

    private void HandleStateMachineState(MachineRuntimeInfo info)
    {
        try
        {
            _machineStates[info.ConfigId] = info;
            OnStateMachineStateChanged?.Invoke(info);
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling state machine state for {MachineId}", info.ConfigId);
        }
    }

    private void HandleFlowRunSummary(FlowRunSummaryMessage summary)
    {
        try
        {
            var runs = _flowRunHistory.GetOrAdd(summary.FlowId, _ => new List<FlowRunSummaryMessage>());
            lock (runs)
            {
                runs.Add(summary);
                if (runs.Count > 200)
                    runs.RemoveRange(0, runs.Count - 200);
            }
            OnFlowRunSummaryReceived?.Invoke(summary);
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling flow run summary for {FlowId}", summary.FlowId);
        }
    }

    private void HandleFlowDeployStatus(FlowDeploymentStatusMessage status)
    {
        try
        {
            _flowDeployStatuses[status.FlowId] = status;
            OnFlowDeployStatusChanged?.Invoke(status.FlowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling deploy status for flow {FlowId}", status.FlowId);
        }
    }

    /// <summary>
    /// Gets the Engine's deployment status for a flow, or null if not yet received.
    /// </summary>
    public FlowDeploymentStatusMessage? GetFlowDeployStatus(string flowId)
    {
        _flowDeployStatuses.TryGetValue(flowId, out var status);
        return status;
    }

    /// <summary>
    /// Compares a local FlowConfig's content hash against the Engine's deployed hash.
    /// Returns a human-readable deployment state.
    /// </summary>
    public FlowDeployState GetFlowDeployState(FlowConfig localFlow)
    {
        if (!_flowDeployStatuses.TryGetValue(localFlow.Id, out var deployed))
            return FlowDeployState.Unknown;  // Engine hasn't reported yet

        var localHash = localFlow.ComputeContentHash();
        if (localHash == deployed.ConfigHash)
            return deployed.IsCompiled ? FlowDeployState.Deployed : FlowDeployState.DeployedDisabled;

        return FlowDeployState.Modified;
    }

    public void Dispose()
    {
        _mqttService.OnTagValueReceived -= HandleTagValue;
        _mqttService.OnBulkTagValuesReceived -= HandleBulkTagValues;
        _mqttService.OnConnectionStatusReceived -= HandleConnectionStatus;
        _mqttService.OnEngineStatusReceived -= HandleEngineStatus;
        _mqttService.OnFlowExecutionReceived -= HandleFlowExecution;
        _mqttService.OnStateMachineStateReceived -= HandleStateMachineState;
        _mqttService.OnFlowRunSummaryReceived -= HandleFlowRunSummary;
        _mqttService.OnFlowDeployStatusReceived -= HandleFlowDeployStatus;
    }
}

/// <summary>
/// Indicates whether the local flow config matches what the Engine has deployed.
/// </summary>
public enum FlowDeployState
{
    /// <summary>Engine hasn't reported status yet (e.g. MQTT not connected).</summary>
    Unknown,
    /// <summary>Local config matches Engine's compiled flow — everything in sync.</summary>
    Deployed,
    /// <summary>Config matches but flow is disabled (compiled hash matches, not running).</summary>
    DeployedDisabled,
    /// <summary>Local config differs from Engine's last-compiled version — needs redeployment.</summary>
    Modified
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
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetDouble().ToString("F2"),
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? "N/A",
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.True => "True",
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.False => "False",
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
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetDouble(),
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(je.GetString(), out var parsed) => parsed,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.True => 1.0,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.False => 0.0,
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

/// <summary>
/// Cached internal tag value.
/// </summary>
public class InternalTagValueCache
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
