// DataForeman Platform - AI Agent Implementation Directives
// Section 4: GRAPH, VALIDATION & EXECUTION
// Validate before runtime execution. Fail before execution, never during.

using DataForeman.Shared.Definition;

namespace DataForeman.Shared.Runtime;

/// <summary>
/// Flow validation result.
/// </summary>
public sealed record FlowValidationResult
{
    /// <summary>Whether the flow is valid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Validation errors (if any).</summary>
    public required IReadOnlyList<FlowValidationError> Errors { get; init; }

    /// <summary>Validation warnings.</summary>
    public IReadOnlyList<FlowValidationWarning> Warnings { get; init; } = Array.Empty<FlowValidationWarning>();

    public static FlowValidationResult Success() => new()
    {
        IsValid = true,
        Errors = Array.Empty<FlowValidationError>()
    };

    public static FlowValidationResult Failure(params FlowValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

/// <summary>
/// Validation error.
/// </summary>
public sealed record FlowValidationError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? NodeId { get; init; }
    public string? WireId { get; init; }
}

/// <summary>
/// Validation warning.
/// </summary>
public sealed record FlowValidationWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? NodeId { get; init; }
}

/// <summary>
/// Flow validator interface.
/// Validates flow definitions before compilation.
/// </summary>
public interface IFlowValidator
{
    /// <summary>
    /// Validates a flow definition.
    /// Must check:
    /// - All referenced node types exist
    /// - All ports exist
    /// - Port cardinality is respected
    /// - No dangling wires
    /// - Cycles only allowed via delay/control nodes
    /// - Disabled nodes are handled safely
    /// </summary>
    FlowValidationResult Validate(FlowDefinition flow, INodeRegistry nodeRegistry);
}

/// <summary>
/// Node registry interface.
/// Provides access to registered node descriptors.
/// </summary>
public interface INodeRegistry
{
    /// <summary>Gets descriptor for a node type.</summary>
    NodeDescriptor? GetDescriptor(string nodeType);

    /// <summary>Gets all registered descriptors.</summary>
    IReadOnlyList<NodeDescriptor> GetAllDescriptors();

    /// <summary>Gets descriptors by category.</summary>
    IReadOnlyList<NodeDescriptor> GetDescriptorsByCategory(string category);

    /// <summary>Checks if a node type is registered.</summary>
    bool IsRegistered(string nodeType);
}

/// <summary>
/// Compiled flow ready for execution.
/// </summary>
public sealed class CompiledFlow
{
    /// <summary>Original flow definition.</summary>
    public required FlowDefinition Definition { get; init; }

    /// <summary>Compiled nodes indexed by ID.</summary>
    public required IReadOnlyDictionary<string, CompiledNode> Nodes { get; init; }

    /// <summary>Trigger nodes that start execution.</summary>
    public required IReadOnlyList<CompiledNode> TriggerNodes { get; init; }

    /// <summary>Adjacency list: node ID -> list of (output port, target node ID, target port).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<WireConnection>> Connections { get; init; }
}

/// <summary>
/// Compiled node with resolved runtime.
/// </summary>
public sealed class CompiledNode
{
    /// <summary>Node definition.</summary>
    public required NodeDefinition Definition { get; init; }

    /// <summary>Node descriptor.</summary>
    public required NodeDescriptor Descriptor { get; init; }

    /// <summary>Node runtime instance.</summary>
    public required INodeRuntime Runtime { get; init; }
}

/// <summary>
/// Wire connection in compiled flow.
/// </summary>
public sealed record WireConnection
{
    public required string SourcePort { get; init; }
    public required string TargetNodeId { get; init; }
    public required string TargetPort { get; init; }
}

/// <summary>
/// Flow compiler interface.
/// Compiles validated flow definitions into executable graphs.
/// </summary>
public interface IFlowCompiler
{
    /// <summary>
    /// Compiles a validated flow definition.
    /// </summary>
    CompiledFlow Compile(FlowDefinition flow, INodeRegistry nodeRegistry, INodeRuntimeFactory runtimeFactory);
}

/// <summary>
/// Factory for creating node runtime instances.
/// </summary>
public interface INodeRuntimeFactory
{
    /// <summary>Creates a runtime instance for a node type.</summary>
    INodeRuntime CreateRuntime(string nodeType);
}
