using System.Text.Json.Serialization;

namespace DataForeman.Shared.Mqtt;

/// <summary>
/// MQTT message containing real-time tag values.
/// Published by Engine to topic: dataforeman/tags/{connectionId}/{tagId}
/// </summary>
public class TagValueMessage
{
    public string ConnectionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string DataType { get; set; } = "Float";
    public int Quality { get; set; } = 0; // 0 = Good, 192 = Bad
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT message for bulk tag value updates.
/// Published by Engine to topic: dataforeman/tags/{connectionId}/bulk
/// </summary>
public class BulkTagValueMessage
{
    public string ConnectionId { get; set; } = string.Empty;
    public List<TagValueMessage> Tags { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT message for connection status updates.
/// Published by Engine to topic: dataforeman/status/{connectionId}
/// </summary>
public class ConnectionStatusMessage
{
    public string ConnectionId { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public ConnectionState State { get; set; }
    public string? ErrorMessage { get; set; }
    public int ActiveTagCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Connection state enumeration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
    Disabled
}

/// <summary>
/// MQTT message for engine status updates.
/// Published by Engine to topic: dataforeman/engine/status
/// </summary>
public class EngineStatusMessage
{
    public bool IsRunning { get; set; }
    public int ActiveConnections { get; set; }
    public int ActiveTags { get; set; }
    public long TotalPolls { get; set; }
    public double AveragePollTimeMs { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT message for configuration reload requests.
/// Published by App to topic: dataforeman/commands/reload
/// </summary>
public class ConfigReloadMessage
{
    public string ConfigType { get; set; } = "all"; // all, connections, charts, flows
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT message summarising a completed flow execution run.
/// Published by Engine to topic: dataforeman/flows/{flowId}/run-summary
/// </summary>
public class FlowRunSummaryMessage
{
    public string FlowId { get; set; } = string.Empty;
    public string FlowName { get; set; } = string.Empty;
    public string TriggerNodeId { get; set; } = string.Empty;
    public string TriggerTopic { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;  // Success, Failure, Timeout
    public int NodesExecuted { get; set; }
    public int MessagesHandled { get; set; }
    public double DurationMs { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT message reporting the deployment status of a flow on the Engine.
/// Published by Engine to topic: dataforeman/flows/{flowId}/deploy-status
/// The App compares the ConfigHash against its local FlowConfig.ComputeContentHash()
/// to detect drift between UI edits and what is actually running.
/// </summary>
public class FlowDeploymentStatusMessage
{
    public string FlowId { get; set; } = string.Empty;
    public string FlowName { get; set; } = string.Empty;
    /// <summary>SHA-256-based content hash of the flow definition as compiled by the Engine.</summary>
    public string ConfigHash { get; set; } = string.Empty;
    /// <summary>Whether the flow was successfully compiled and is running.</summary>
    public bool IsCompiled { get; set; }
    /// <summary>Whether the flow was enabled in the loaded config.</summary>
    public bool IsEnabled { get; set; }
    public int NodeCount { get; set; }
    public DateTime CompiledAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MQTT topics used by DataForeman.
/// </summary>
public static class MqttTopics
{
    /// <summary>Common prefix for all internal DataForeman topics.</summary>
    public const string InternalPrefix = "dataforeman/";

    // Tag value topics
    public const string TagValuePattern = "dataforeman/tags/{connectionId}/{tagId}";
    public const string BulkTagValuePattern = "dataforeman/tags/{connectionId}/bulk";
    public const string AllTagsWildcard = "dataforeman/tags/#";
    
    // Status topics
    public const string ConnectionStatusPattern = "dataforeman/status/{connectionId}";
    public const string AllConnectionStatusWildcard = "dataforeman/status/#";
    public const string EngineStatus = "dataforeman/engine/status";
    
    // Command topics
    public const string ConfigReload = "dataforeman/commands/reload";
    
    // State machine topics
    public const string StateMachineStatePattern = "dataforeman/statemachines/{machineId}/state";
    public const string AllStateMachineStateWildcard = "dataforeman/statemachines/+/state";
    
    // Flow execution summary topics
    public const string FlowRunSummaryPattern = "dataforeman/flows/{flowId}/run-summary";
    public const string AllFlowRunSummaryWildcard = "dataforeman/flows/+/run-summary";
    
    // Flow deployment status topics
    public const string FlowDeployStatusPattern = "dataforeman/flows/{flowId}/deploy-status";
    public const string AllFlowDeployStatusWildcard = "dataforeman/flows/+/deploy-status";
    
    public static string GetTagValueTopic(string connectionId, string tagId) 
        => $"dataforeman/tags/{connectionId}/{tagId}";
    
    public static string GetBulkTagValueTopic(string connectionId) 
        => $"dataforeman/tags/{connectionId}/bulk";
    
    public static string GetConnectionStatusTopic(string connectionId) 
        => $"dataforeman/status/{connectionId}";
    
    public static string GetStateMachineStateTopic(string machineId) 
        => $"dataforeman/statemachines/{machineId}/state";
    
    public static string GetFlowRunSummaryTopic(string flowId) 
        => $"dataforeman/flows/{flowId}/run-summary";
    
    public static string GetFlowDeployStatusTopic(string flowId) 
        => $"dataforeman/flows/{flowId}/deploy-status";
    
    // Flow manual trigger topics
    public const string FlowManualTriggerPattern = "dataforeman/flows/{flowId}/trigger";
    public const string AllFlowManualTriggerWildcard = "dataforeman/flows/+/trigger";
    
    public static string GetFlowManualTriggerTopic(string flowId) 
        => $"dataforeman/flows/{flowId}/trigger";
}
