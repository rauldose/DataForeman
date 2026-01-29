using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataForeman.FlowEngine;

/// <summary>
/// Represents a flow definition.
/// </summary>
public class FlowDefinition
{
    /// <summary>
    /// Flow ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Flow name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of nodes in the flow.
    /// </summary>
    public List<FlowNode> Nodes { get; set; } = new();

    /// <summary>
    /// List of edges connecting nodes.
    /// </summary>
    public List<FlowEdge> Edges { get; set; } = new();

    /// <summary>
    /// Execution mode (continuous or manual).
    /// </summary>
    public string ExecutionMode { get; set; } = "continuous";

    /// <summary>
    /// Scan rate in milliseconds.
    /// </summary>
    public int ScanRateMs { get; set; } = 1000;
}

/// <summary>
/// Represents a node in a flow.
/// </summary>
public class FlowNode
{
    /// <summary>
    /// Unique node ID within the flow.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Node type identifier.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Display label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// X position in the editor.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y position in the editor.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Node-specific configuration.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Config { get; set; }
}

/// <summary>
/// Represents an edge connecting two nodes.
/// </summary>
public class FlowEdge
{
    /// <summary>
    /// Edge ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Source node ID.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Source handle/port name.
    /// </summary>
    public string? SourceHandle { get; set; }

    /// <summary>
    /// Target node ID.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Target handle/port name.
    /// </summary>
    public string? TargetHandle { get; set; }
}

/// <summary>
/// Execution context for a flow run.
/// </summary>
public class FlowExecutionContext
{
    /// <summary>
    /// Flow ID.
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    /// Execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Node outputs indexed by node ID.
    /// </summary>
    public Dictionary<string, object?> NodeOutputs { get; set; } = new();

    /// <summary>
    /// Runtime parameters.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Execution start time.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Cancellation token for the execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Get the output of a node.
    /// </summary>
    public T? GetNodeOutput<T>(string nodeId)
    {
        if (NodeOutputs.TryGetValue(nodeId, out var output) && output is T typedOutput)
        {
            return typedOutput;
        }
        return default;
    }

    /// <summary>
    /// Set the output of a node.
    /// </summary>
    public void SetNodeOutput(string nodeId, object? value)
    {
        NodeOutputs[nodeId] = value;
    }
}

/// <summary>
/// Result of a node execution.
/// </summary>
public class NodeExecutionResult
{
    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Output value(s) from the node.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public int ExecutionTimeMs { get; set; }

    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static NodeExecutionResult Ok(object? output = null) =>
        new() { Success = true, Output = output };

    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static NodeExecutionResult Fail(string error) =>
        new() { Success = false, Error = error };
}
