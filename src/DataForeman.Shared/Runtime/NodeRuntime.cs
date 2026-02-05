// DataForeman Platform - AI Agent Implementation Directives
// Section 2.5: NODE RUNTIME
// Execution interface and context. Pure execution contract.
// Forbidden in runtime: UI access, file system access, global state, blocking calls.

using System.Text.Json;
using DataForeman.Shared.Definition;

namespace DataForeman.Shared.Runtime;

/// <summary>
/// Interface for node runtime execution.
/// Every node type must implement this interface.
/// </summary>
public interface INodeRuntime
{
    /// <summary>
    /// Executes the node logic.
    /// Must be pure and deterministic given the same inputs.
    /// </summary>
    ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct);
}

/// <summary>
/// Execution context provided to node runtime.
/// Contains everything the node needs to execute.
/// </summary>
public sealed class NodeExecutionContext
{
    /// <summary>The node definition being executed.</summary>
    public required NodeDefinition Node { get; init; }

    /// <summary>The node descriptor for this node type.</summary>
    public required NodeDescriptor Descriptor { get; init; }

    /// <summary>Validated configuration for this node instance.</summary>
    public required JsonElement? Config { get; init; }

    /// <summary>The incoming message that triggered execution.</summary>
    public required MessageEnvelope Message { get; init; }

    /// <summary>Current UTC time (injected for determinism).</summary>
    public required DateTime CurrentUtc { get; init; }

    /// <summary>Run ID for this execution chain.</summary>
    public required string RunId { get; init; }

    /// <summary>The flow ID this node belongs to.</summary>
    public string? FlowId { get; init; }

    /// <summary>Emitter for sending messages to output ports.</summary>
    public required IMessageEmitter Emitter { get; init; }

    /// <summary>Logger for node execution.</summary>
    public required INodeLogger Logger { get; init; }

    /// <summary>Historian writer for time-series data.</summary>
    public required IHistorianWriter Historian { get; init; }

    /// <summary>Tag value reader for accessing current tag values.</summary>
    public required ITagValueReader TagReader { get; init; }

    /// <summary>Tag value writer for writing tag values.</summary>
    public required ITagValueWriter TagWriter { get; init; }

    /// <summary>Optional MQTT publisher for mqtt-out nodes.</summary>
    public INodeMqttPublisher? MqttPublisher { get; init; }

    /// <summary>Optional context store for internal tags (global/flow/node scopes).</summary>
    public IContextStore? ContextStore { get; init; }

    /// <summary>Gets typed configuration.</summary>
    public T? GetConfig<T>() where T : class
    {
        if (Config == null || Config.Value.ValueKind == JsonValueKind.Null)
            return null;
        return Config.Value.Deserialize<T>();
    }
}

/// <summary>
/// Interface for MQTT publishing from nodes.
/// </summary>
public interface INodeMqttPublisher
{
    /// <summary>
    /// Publishes a message to an MQTT topic.
    /// </summary>
    ValueTask PublishAsync(string topic, string payload, int qos = 0, bool retain = false, CancellationToken ct = default);
}

/// <summary>
/// Interface for accessing context store (internal tags) from nodes.
/// Provides global, flow, and node-scoped context similar to Node-RED.
/// </summary>
public interface IContextStore
{
    /// <summary>Gets a value from global context.</summary>
    object? GetGlobal(string key);
    
    /// <summary>Sets a value in global context.</summary>
    void SetGlobal(string key, object? value);
    
    /// <summary>Gets all keys in global context.</summary>
    IEnumerable<string> GetGlobalKeys();
    
    /// <summary>Gets a value from flow context.</summary>
    object? GetFlow(string key);
    
    /// <summary>Sets a value in flow context.</summary>
    void SetFlow(string key, object? value);
    
    /// <summary>Gets all keys in flow context.</summary>
    IEnumerable<string> GetFlowKeys();
    
    /// <summary>Gets a value from node context.</summary>
    object? GetNode(string key);
    
    /// <summary>Sets a value in node context.</summary>
    void SetNode(string key, object? value);
    
    /// <summary>Gets all keys in node context.</summary>
    IEnumerable<string> GetNodeKeys();
}

/// <summary>
/// Interface for emitting messages to output ports.
/// </summary>
public interface IMessageEmitter
{
    /// <summary>
    /// Emits a message to the specified output port.
    /// </summary>
    void Emit(string portName, MessageEnvelope message);

    /// <summary>
    /// Emits an error to the error port (if exists).
    /// </summary>
    void EmitError(Exception error, MessageEnvelope originalMessage);
}

/// <summary>
/// Interface for node logging.
/// </summary>
public interface INodeLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// Interface for historian writes (flows emit, never query).
/// </summary>
public interface IHistorianWriter
{
    /// <summary>
    /// Writes a measurement to the historian.
    /// Append-only, no queries allowed from nodes.
    /// </summary>
    ValueTask WriteAsync(HistorianMeasurement measurement, CancellationToken ct);
}

/// <summary>
/// Historian measurement record.
/// </summary>
public sealed record HistorianMeasurement
{
    /// <summary>Measurement name/tag path.</summary>
    public required string Name { get; init; }

    /// <summary>Measurement value.</summary>
    public required double Value { get; init; }

    /// <summary>UTC timestamp.</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>Optional quality indicator.</summary>
    public int Quality { get; init; } = 192; // Good quality

    /// <summary>Optional tags/labels.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Interface for reading tag values.
/// </summary>
public interface ITagValueReader
{
    /// <summary>Gets current value of a tag.</summary>
    ValueTask<TagValue?> GetValueAsync(string tagPath, CancellationToken ct);
}

/// <summary>
/// Interface for writing tag values.
/// </summary>
public interface ITagValueWriter
{
    /// <summary>Writes a value to a tag.</summary>
    ValueTask WriteValueAsync(string tagPath, object value, CancellationToken ct);
}

/// <summary>
/// Tag value with timestamp and quality.
/// </summary>
public sealed record TagValue
{
    public required string TagPath { get; init; }
    public required object? Value { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required int Quality { get; init; }

    public double AsDouble() => Convert.ToDouble(Value);
    public string AsString() => Value?.ToString() ?? string.Empty;
    public bool AsBool() => Convert.ToBoolean(Value);
}

/// <summary>
/// Node execution result for tracing.
/// Every node execution must emit this.
/// </summary>
public sealed record NodeExecutionResult
{
    /// <summary>Run ID for this execution chain.</summary>
    public required string RunId { get; init; }

    /// <summary>Node ID that was executed.</summary>
    public required string NodeId { get; init; }

    /// <summary>Node type.</summary>
    public required string NodeType { get; init; }

    /// <summary>Message ID that triggered execution.</summary>
    public required string MessageId { get; init; }

    /// <summary>Correlation ID.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Execution start time (UTC).</summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>Execution end time (UTC).</summary>
    public required DateTime EndUtc { get; init; }

    /// <summary>Execution duration.</summary>
    public TimeSpan Duration => EndUtc - StartUtc;

    /// <summary>Execution status.</summary>
    public required ExecutionStatus Status { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Number of messages emitted.</summary>
    public int MessagesEmitted { get; init; }

    /// <summary>Parent trace ID for subflow nesting.</summary>
    public string? ParentTraceId { get; init; }
}

/// <summary>
/// Execution status.
/// </summary>
public enum ExecutionStatus
{
    Success,
    Failed,
    Skipped,
    Timeout
}
