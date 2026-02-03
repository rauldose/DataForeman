namespace DataForeman.Shared.Models;

/// <summary>
/// Real-time tag value update (published by Engine)
/// Topic: dataforeman/tags/{connectionId}/{tagId}
/// </summary>
public class TagValueMessage
{
    public string ConnectionId { get; set; } = "";
    public string TagId { get; set; } = "";
    public object? Value { get; set; }
    public TagQuality Quality { get; set; } = TagQuality.Good;
    public long Timestamp { get; set; }  // Unix milliseconds

    public DateTime GetDateTime() => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).UtcDateTime;
}

public enum TagQuality
{
    Good = 0,
    Bad = 1,
    Uncertain = 2,
    NotConnected = 3
}

/// <summary>
/// Historical data for a tag (response to history request)
/// Topic: dataforeman/history/{connectionId}/{tagId}
/// </summary>
public class TagHistoryMessage
{
    public string ConnectionId { get; set; } = "";
    public string TagId { get; set; } = "";
    public List<TagDataPoint> Points { get; set; } = new();
}

public class TagDataPoint
{
    public long Timestamp { get; set; }
    public object? Value { get; set; }
    public TagQuality Quality { get; set; }
}

/// <summary>
/// Request historical data
/// Topic: dataforeman/history/request
/// </summary>
public class HistoryRequestMessage
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string ConnectionId { get; set; } = "";
    public string TagId { get; set; } = "";
    public long StartTime { get; set; }  // Unix milliseconds
    public long EndTime { get; set; }
    public int MaxPoints { get; set; } = 1000;
    public AggregationType Aggregation { get; set; } = AggregationType.None;
    public int? AggregationIntervalMs { get; set; }
}

/// <summary>
/// Engine status update
/// Topic: dataforeman/engine/status
/// </summary>
public class EngineStatusMessage
{
    public bool Running { get; set; }
    public int ActiveConnections { get; set; }
    public int ActiveTags { get; set; }
    public int ActiveFlows { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long ScanCount { get; set; }
    public double AverageScanTimeMs { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Flow execution event
/// Topic: dataforeman/flows/{flowId}/execution
/// </summary>
public class FlowExecutionMessage
{
    public string FlowId { get; set; } = "";
    public string ExecutionId { get; set; } = "";
    public FlowExecutionStatus Status { get; set; }
    public string? NodeId { get; set; }  // Current node being executed
    public string? Error { get; set; }
    public long Timestamp { get; set; }
    public double DurationMs { get; set; }
}

public enum FlowExecutionStatus
{
    Started,
    NodeExecuting,
    NodeCompleted,
    Completed,
    Error
}

/// <summary>
/// Command from App to Engine
/// Topic: dataforeman/command
/// </summary>
public class EngineCommandMessage
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public EngineCommand Command { get; set; }
    public Dictionary<string, object?>? Parameters { get; set; }
}

public enum EngineCommand
{
    ReloadConfig,
    StartConnection,
    StopConnection,
    StartFlow,
    StopFlow,
    RequestHistory,
    Shutdown
}

/// <summary>
/// MQTT topic patterns
/// </summary>
public static class MqttTopics
{
    public const string TagValuePattern = "dataforeman/tags/{0}/{1}";  // connectionId, tagId
    public const string TagValueWildcard = "dataforeman/tags/#";
    public const string HistoryResponse = "dataforeman/history/{0}/{1}";
    public const string HistoryRequest = "dataforeman/history/request";
    public const string EngineStatus = "dataforeman/engine/status";
    public const string FlowExecution = "dataforeman/flows/{0}/execution";
    public const string FlowExecutionWildcard = "dataforeman/flows/+/execution";
    public const string Command = "dataforeman/command";

    public static string GetTagTopic(string connectionId, string tagId)
        => string.Format(TagValuePattern, connectionId, tagId);

    public static string GetHistoryTopic(string connectionId, string tagId)
        => string.Format(HistoryResponse, connectionId, tagId);

    public static string GetFlowTopic(string flowId)
        => string.Format(FlowExecution, flowId);
}
