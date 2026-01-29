using System.Text.Json;

namespace DataForeman.RedisStreams;

/// <summary>
/// Represents a telemetry data point from industrial equipment.
/// </summary>
public class TelemetryMessage
{
    /// <summary>
    /// Connection ID identifying the data source.
    /// </summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// Tag ID within the connection.
    /// </summary>
    public int TagId { get; set; }

    /// <summary>
    /// Timestamp of the telemetry reading (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The value of the tag (can be numeric, string, or boolean).
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Quality code (0 = Good, 1+ = Bad/Uncertain).
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Serialize to JSON for Redis stream storage.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserialize from JSON.
    /// </summary>
    public static TelemetryMessage? FromJson(string json) =>
        JsonSerializer.Deserialize<TelemetryMessage>(json);
}

/// <summary>
/// Represents a flow execution event for the flow engine.
/// </summary>
public class FlowExecutionMessage
{
    /// <summary>
    /// The flow ID to execute.
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    /// The execution session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Trigger node ID that initiated the execution.
    /// </summary>
    public string? TriggerNodeId { get; set; }

    /// <summary>
    /// Runtime parameters as JSON.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Timestamp when the execution was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// Serialize to JSON.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserialize from JSON.
    /// </summary>
    public static FlowExecutionMessage? FromJson(string json) =>
        JsonSerializer.Deserialize<FlowExecutionMessage>(json);
}

/// <summary>
/// Represents a stream entry read from Redis.
/// </summary>
public class StreamEntry
{
    /// <summary>
    /// Redis stream message ID.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The stream name this entry came from.
    /// </summary>
    public string StreamName { get; set; } = string.Empty;

    /// <summary>
    /// Message data as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();
}
