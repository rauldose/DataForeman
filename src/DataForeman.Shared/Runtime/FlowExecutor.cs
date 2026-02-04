// DataForeman Platform - AI Agent Implementation Directives
// Section 4.2: RUNTIME EXECUTION MODEL
// Push-based message routing, async execution, cancellation supported.
// Explicit error paths (error ports). No implicit exception swallowing.

namespace DataForeman.Shared.Runtime;

/// <summary>
/// Flow executor interface.
/// Executes compiled flows with push-based message routing.
/// </summary>
public interface IFlowExecutor
{
    /// <summary>
    /// Executes a compiled flow starting from a trigger node.
    /// </summary>
    ValueTask<FlowExecutionResult> ExecuteAsync(
        CompiledFlow flow,
        string triggerNodeId,
        MessageEnvelope initialMessage,
        FlowExecutionOptions options,
        CancellationToken ct);

    /// <summary>
    /// Executes a compiled flow starting from a specific node (for testing).
    /// </summary>
    ValueTask<FlowExecutionResult> ExecuteFromNodeAsync(
        CompiledFlow flow,
        string startNodeId,
        MessageEnvelope message,
        FlowExecutionOptions options,
        CancellationToken ct);
}

/// <summary>
/// Flow execution options.
/// </summary>
public sealed record FlowExecutionOptions
{
    /// <summary>Maximum execution time for the entire flow.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum messages to process (circuit breaker).</summary>
    public int MaxMessages { get; init; } = 1000;

    /// <summary>Whether to stop on first error.</summary>
    public bool StopOnError { get; init; }

    /// <summary>Run ID (generated if not provided).</summary>
    public string? RunId { get; init; }

    /// <summary>Parent trace ID for subflow execution.</summary>
    public string? ParentTraceId { get; init; }
}

/// <summary>
/// Flow execution result.
/// </summary>
public sealed record FlowExecutionResult
{
    /// <summary>Run ID for this execution.</summary>
    public required string RunId { get; init; }

    /// <summary>Flow ID.</summary>
    public required string FlowId { get; init; }

    /// <summary>Execution start time.</summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>Execution end time.</summary>
    public required DateTime EndUtc { get; init; }

    /// <summary>Total execution duration.</summary>
    public TimeSpan Duration => EndUtc - StartUtc;

    /// <summary>Overall execution status.</summary>
    public required ExecutionStatus Status { get; init; }

    /// <summary>All node execution traces.</summary>
    public required IReadOnlyList<NodeExecutionResult> Traces { get; init; }

    /// <summary>Total messages processed.</summary>
    public int MessagesProcessed { get; init; }

    /// <summary>Nodes that succeeded.</summary>
    public int NodesSucceeded { get; init; }

    /// <summary>Nodes that failed.</summary>
    public int NodesFailed { get; init; }

    /// <summary>Nodes that were skipped.</summary>
    public int NodesSkipped { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Execution tracer interface.
/// Every node execution must emit traces. No node may opt out.
/// </summary>
public interface IExecutionTracer
{
    /// <summary>Records a node execution trace.</summary>
    void RecordTrace(NodeExecutionResult trace);

    /// <summary>Gets all traces for a run.</summary>
    IReadOnlyList<NodeExecutionResult> GetTraces(string runId);

    /// <summary>Clears traces older than the specified time.</summary>
    void ClearOldTraces(DateTime beforeUtc);
}

/// <summary>
/// Time provider interface for deterministic execution.
/// Allows injecting controlled time for testing.
/// </summary>
public interface ITimeProvider
{
    /// <summary>Gets current UTC time.</summary>
    DateTime UtcNow { get; }
}

/// <summary>
/// Default time provider using system clock.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Controllable time provider for testing.
/// </summary>
public sealed class TestTimeProvider : ITimeProvider
{
    private DateTime _utcNow;

    public TestTimeProvider(DateTime initialUtc)
    {
        _utcNow = initialUtc;
    }

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    public void SetTime(DateTime utc) => _utcNow = utc;
}
