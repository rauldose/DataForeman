using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DataForeman.RedisStreams;

namespace DataForeman.FlowEngine;

/// <summary>
/// Interface for the flow execution engine.
/// </summary>
public interface IFlowExecutionEngine
{
    /// <summary>
    /// Execute a flow definition.
    /// </summary>
    Task<FlowExecutionResult> ExecuteAsync(FlowDefinition flow, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a flow by ID (loads definition from database).
    /// </summary>
    Task<FlowExecutionResult> ExecuteByIdAsync(Guid flowId, string? triggerNodeId = null, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a flow execution.
/// </summary>
public class FlowExecutionResult
{
    /// <summary>
    /// Execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// All node outputs.
    /// </summary>
    public Dictionary<string, object?> NodeOutputs { get; set; } = new();

    /// <summary>
    /// Errors that occurred during execution.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public int ExecutionTimeMs { get; set; }

    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Flow execution engine that processes flow definitions.
/// </summary>
public class FlowExecutionEngine : IFlowExecutionEngine
{
    private readonly ILogger<FlowExecutionEngine> _logger;
    private readonly IRedisStreamService? _redisService;
    private readonly Dictionary<string, INodeExecutor> _executors;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the flow execution engine.
    /// </summary>
    public FlowExecutionEngine(
        ILogger<FlowExecutionEngine> logger,
        IServiceProvider serviceProvider,
        IRedisStreamService? redisService = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _redisService = redisService;

        // Register built-in node executors
        _executors = new Dictionary<string, INodeExecutor>();
        RegisterExecutor(new ManualTriggerExecutor());
        RegisterExecutor(new ScheduleTriggerExecutor());
        RegisterExecutor(new TagInputExecutor());
        RegisterExecutor(new TagOutputExecutor());
        RegisterExecutor(new MathAddExecutor());
        RegisterExecutor(new MathSubtractExecutor());
        RegisterExecutor(new MathMultiplyExecutor());
        RegisterExecutor(new MathDivideExecutor());
        RegisterExecutor(new CompareEqualExecutor());
        RegisterExecutor(new CompareGreaterExecutor());
        RegisterExecutor(new CompareLessExecutor());
        RegisterExecutor(new LogicIfExecutor());
        RegisterExecutor(new DebugLogExecutor());
        RegisterExecutor(new CSharpScriptExecutor());
    }

    /// <summary>
    /// Register a node executor.
    /// </summary>
    public void RegisterExecutor(INodeExecutor executor)
    {
        _executors[executor.NodeType] = executor;
    }

    /// <inheritdoc />
    public async Task<FlowExecutionResult> ExecuteAsync(FlowDefinition flow, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var startedAt = DateTime.UtcNow;

        _logger.LogInformation("Starting flow execution {ExecutionId} for flow {FlowId} ({FlowName})", 
            executionId, flow.Id, flow.Name);

        var context = new FlowExecutionContext
        {
            FlowId = flow.Id,
            ExecutionId = executionId,
            SessionId = Guid.NewGuid(),
            Parameters = parameters ?? new Dictionary<string, object?>(),
            StartedAt = startedAt,
            CancellationToken = cancellationToken
        };

        var result = new FlowExecutionResult
        {
            ExecutionId = executionId,
            Success = true,
            Status = "running",
            StartedAt = startedAt
        };

        try
        {
            // Build execution order using topological sort
            var executionOrder = GetExecutionOrder(flow);

            foreach (var nodeId in executionOrder)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Status = "cancelled";
                    result.Success = false;
                    break;
                }

                var node = flow.Nodes.First(n => n.Id == nodeId);
                
                if (!_executors.TryGetValue(node.Type, out var executor))
                {
                    _logger.LogWarning("No executor found for node type {NodeType}", node.Type);
                    result.Errors.Add($"No executor for node type: {node.Type}");
                    continue;
                }

                try
                {
                    var nodeResult = await executor.ExecuteAsync(node, context);
                    
                    if (nodeResult.Success)
                    {
                        context.SetNodeOutput(node.Id, nodeResult.Output);
                        _logger.LogDebug("Node {NodeId} ({NodeType}) executed successfully", node.Id, node.Type);
                    }
                    else
                    {
                        _logger.LogWarning("Node {NodeId} ({NodeType}) failed: {Error}", 
                            node.Id, node.Type, nodeResult.Error);
                        result.Errors.Add($"Node {node.Id}: {nodeResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing node {NodeId} ({NodeType})", node.Id, node.Type);
                    result.Errors.Add($"Node {node.Id}: {ex.Message}");
                }
            }

            result.Status = result.Errors.Count > 0 ? "completed_with_errors" : "completed";
            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flow execution {ExecutionId} failed", executionId);
            result.Success = false;
            result.Status = "failed";
            result.Errors.Add(ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            result.NodeOutputs = context.NodeOutputs;
            result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Flow execution {ExecutionId} completed with status {Status} in {ElapsedMs}ms",
                executionId, result.Status, result.ExecutionTimeMs);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<FlowExecutionResult> ExecuteByIdAsync(Guid flowId, string? triggerNodeId = null, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        // TODO: Load flow definition from database
        // For now, return a placeholder result
        _logger.LogWarning("ExecuteByIdAsync not fully implemented - flow {FlowId} not loaded from database", flowId);

        return await Task.FromResult(new FlowExecutionResult
        {
            ExecutionId = Guid.NewGuid(),
            Success = false,
            Status = "failed",
            Errors = new List<string> { "Flow definition loading not yet implemented" },
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get the execution order of nodes using topological sort.
    /// </summary>
    private List<string> GetExecutionOrder(FlowDefinition flow)
    {
        var order = new List<string>();
        var visited = new HashSet<string>();
        var tempMark = new HashSet<string>();

        // Build adjacency list from edges
        var dependencies = new Dictionary<string, List<string>>();
        foreach (var node in flow.Nodes)
        {
            dependencies[node.Id] = new List<string>();
        }

        foreach (var edge in flow.Edges)
        {
            if (dependencies.ContainsKey(edge.Target))
            {
                dependencies[edge.Target].Add(edge.Source);
            }
        }

        void Visit(string nodeId)
        {
            if (visited.Contains(nodeId)) return;
            if (tempMark.Contains(nodeId))
            {
                throw new InvalidOperationException($"Circular dependency detected at node {nodeId}");
            }

            tempMark.Add(nodeId);

            foreach (var dep in dependencies[nodeId])
            {
                Visit(dep);
            }

            tempMark.Remove(nodeId);
            visited.Add(nodeId);
            order.Add(nodeId);
        }

        foreach (var node in flow.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                Visit(node.Id);
            }
        }

        return order;
    }
}
