// DataForeman Platform - AI Agent Implementation Directives
// Section 4.2: RUNTIME EXECUTION MODEL
// Push-based message routing, async execution, cancellation supported.

using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataForeman.Engine.Runtime;

/// <summary>
/// Flow compiler implementation.
/// </summary>
public sealed class FlowCompiler : IFlowCompiler
{
    public CompiledFlow Compile(FlowDefinition flow, INodeRegistry nodeRegistry, INodeRuntimeFactory runtimeFactory)
    {
        var nodes = new Dictionary<string, CompiledNode>();
        var triggerNodes = new List<CompiledNode>();
        var connections = new Dictionary<string, List<WireConnection>>();

        // Compile all nodes
        foreach (var nodeDef in flow.Nodes.Where(n => !n.Disabled))
        {
            var descriptor = nodeRegistry.GetDescriptor(nodeDef.Type);
            if (descriptor == null)
                throw new InvalidOperationException($"Unknown node type: {nodeDef.Type}");

            var runtime = runtimeFactory.CreateRuntime(nodeDef.Type);
            var compiledNode = new CompiledNode
            {
                Definition = nodeDef,
                Descriptor = descriptor,
                Runtime = runtime
            };

            nodes[nodeDef.Id] = compiledNode;

            if (descriptor.IsTrigger)
                triggerNodes.Add(compiledNode);

            // Initialize connection list
            connections[nodeDef.Id] = new List<WireConnection>();
        }

        // Build connection graph
        foreach (var wire in flow.Wires)
        {
            // Skip wires connected to disabled nodes
            if (!nodes.ContainsKey(wire.SourceNodeId) || !nodes.ContainsKey(wire.TargetNodeId))
                continue;

            connections[wire.SourceNodeId].Add(new WireConnection
            {
                SourcePort = wire.SourcePort,
                TargetNodeId = wire.TargetNodeId,
                TargetPort = wire.TargetPort
            });
        }

        return new CompiledFlow
        {
            Definition = flow,
            Nodes = nodes,
            TriggerNodes = triggerNodes,
            Connections = connections.ToDictionary(
                kvp => kvp.Key, 
                kvp => (IReadOnlyList<WireConnection>)kvp.Value.AsReadOnly())
        };
    }
}

/// <summary>
/// Flow executor implementation with push-based message routing.
/// </summary>
public sealed class FlowExecutor : IFlowExecutor
{
    private readonly ITimeProvider _timeProvider;
    private readonly IExecutionTracer _tracer;
    private readonly IHistorianWriter _historian;
    private readonly ITagValueReader _tagReader;
    private readonly ITagValueWriter _tagWriter;
    private readonly INodeMqttPublisher? _mqttPublisher;
    private readonly IContextStore? _contextStore;
    private readonly ILogger<FlowExecutor> _logger;

    public FlowExecutor(
        ITimeProvider timeProvider,
        IExecutionTracer tracer,
        IHistorianWriter historian,
        ITagValueReader tagReader,
        ITagValueWriter tagWriter,
        ILogger<FlowExecutor> logger,
        INodeMqttPublisher? mqttPublisher = null,
        IContextStore? contextStore = null)
    {
        _timeProvider = timeProvider;
        _tracer = tracer;
        _historian = historian;
        _tagReader = tagReader;
        _tagWriter = tagWriter;
        _logger = logger;
        _mqttPublisher = mqttPublisher;
        _contextStore = contextStore;
    }

    public async ValueTask<FlowExecutionResult> ExecuteAsync(
        CompiledFlow flow,
        string triggerNodeId,
        MessageEnvelope initialMessage,
        FlowExecutionOptions options,
        CancellationToken ct)
    {
        return await ExecuteFromNodeAsync(flow, triggerNodeId, initialMessage, options, ct);
    }

    public async ValueTask<FlowExecutionResult> ExecuteFromNodeAsync(
        CompiledFlow flow,
        string startNodeId,
        MessageEnvelope message,
        FlowExecutionOptions options,
        CancellationToken ct)
    {
        var runId = options.RunId ?? Guid.NewGuid().ToString("N");
        var startUtc = _timeProvider.UtcNow;
        var traces = new List<NodeExecutionResult>();
        var messageQueue = new Queue<(string NodeId, string PortName, MessageEnvelope Message)>();
        var messagesProcessed = 0;
        var nodesSucceeded = 0;
        var nodesFailed = 0;
        var nodesSkipped = 0;
        string? error = null;
        var status = ExecutionStatus.Success;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(options.Timeout);

        try
        {
            // Start with the initial node
            messageQueue.Enqueue((startNodeId, "input", message));

            while (messageQueue.Count > 0 && messagesProcessed < options.MaxMessages)
            {
                cts.Token.ThrowIfCancellationRequested();

                var (nodeId, portName, msg) = messageQueue.Dequeue();
                messagesProcessed++;

                if (!flow.Nodes.TryGetValue(nodeId, out var node))
                {
                    _logger.LogWarning("Node {NodeId} not found in compiled flow", nodeId);
                    continue;
                }

                var nodeStartUtc = _timeProvider.UtcNow;
                var emitter = new MessageEmitter(_timeProvider, nodeId, flow.Connections[nodeId]);
                var nodeLogger = new NodeLogger(_logger, node.Definition.Name, nodeId);

                try
                {
                    // Capture input payload for live value annotations
                    Dictionary<string, object>? inputSnapshot = null;
                    try
                    {
                        var inPayload = msg.Payload;
                        if (inPayload.HasValue)
                        {
                            var inVal = inPayload.Value;
                            if (inVal.ValueKind == JsonValueKind.Object)
                            {
                                inputSnapshot = new Dictionary<string, object>();
                                foreach (var prop in inVal.EnumerateObject())
                                {
                                    inputSnapshot[prop.Name] = prop.Value.ValueKind switch
                                    {
                                        JsonValueKind.Number => prop.Value.GetDouble(),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        JsonValueKind.String => prop.Value.GetString() ?? "",
                                        _ => prop.Value.ToString()
                                    };
                                }
                            }
                            else if (inVal.ValueKind != JsonValueKind.Undefined &&
                                     inVal.ValueKind != JsonValueKind.Null)
                            {
                                inputSnapshot = new Dictionary<string, object>
                                {
                                    ["value"] = inVal.ToString()
                                };
                            }
                        }
                    }
                    catch (JsonException) { }
                    catch (InvalidOperationException) { }

                    var context = new NodeExecutionContext
                    {
                        Node = node.Definition,
                        Descriptor = node.Descriptor,
                        Config = node.Definition.Config,
                        Message = msg,
                        CurrentUtc = _timeProvider.UtcNow,
                        RunId = runId,
                        FlowId = flow.Definition.Id,
                        Emitter = emitter,
                        Logger = nodeLogger,
                        Historian = _historian,
                        TagReader = _tagReader,
                        TagWriter = _tagWriter,
                        MqttPublisher = _mqttPublisher,
                        ContextStore = _contextStore
                    };

                    await node.Runtime.ExecuteAsync(context, cts.Token);

                    var nodeEndUtc = _timeProvider.UtcNow;

                    // Capture first output payload for live value annotations
                    Dictionary<string, object>? outputSnapshot = null;
                    if (emitter.EmittedMessages.Count > 0)
                    {
                        try
                        {
                            var nullablePayload = emitter.EmittedMessages[0].Message.Payload;
                            if (nullablePayload.HasValue)
                            {
                                var payload = nullablePayload.Value;
                                if (payload.ValueKind == JsonValueKind.Object)
                                {
                                    outputSnapshot = new Dictionary<string, object>();
                                    foreach (var prop in payload.EnumerateObject())
                                    {
                                        outputSnapshot[prop.Name] = prop.Value.ValueKind switch
                                        {
                                            JsonValueKind.Number => prop.Value.GetDouble(),
                                            JsonValueKind.True => true,
                                            JsonValueKind.False => false,
                                            JsonValueKind.String => prop.Value.GetString() ?? "",
                                            _ => prop.Value.ToString()
                                        };
                                    }
                                }
                                else if (payload.ValueKind != JsonValueKind.Undefined &&
                                         payload.ValueKind != JsonValueKind.Null)
                                {
                                    outputSnapshot = new Dictionary<string, object>
                                    {
                                        ["value"] = payload.ToString()
                                    };
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Payload couldn't be deserialized — skip output capture
                        }
                        catch (InvalidOperationException)
                        {
                            // Payload structure unexpected — skip output capture
                        }
                    }

                    var trace = new NodeExecutionResult
                    {
                        RunId = runId,
                        FlowId = flow.Definition.Id,
                        NodeId = nodeId,
                        NodeType = node.Definition.Type,
                        MessageId = msg.MessageId,
                        CorrelationId = msg.CorrelationId,
                        StartUtc = nodeStartUtc,
                        EndUtc = nodeEndUtc,
                        Status = ExecutionStatus.Success,
                        MessagesEmitted = emitter.EmittedMessages.Count,
                        ParentTraceId = options.ParentTraceId,
                        InputValues = inputSnapshot,
                        OutputValues = outputSnapshot
                    };

                    traces.Add(trace);
                    _tracer.RecordTrace(trace);
                    nodesSucceeded++;

                    // Queue emitted messages
                    foreach (var emittedMsg in emitter.EmittedMessages)
                    {
                        messageQueue.Enqueue((emittedMsg.TargetNodeId, emittedMsg.TargetPort, emittedMsg.Message));
                    }
                }
                catch (Exception ex)
                {
                    var nodeEndUtc = _timeProvider.UtcNow;
                    var trace = new NodeExecutionResult
                    {
                        RunId = runId,
                        FlowId = flow.Definition.Id,
                        NodeId = nodeId,
                        NodeType = node.Definition.Type,
                        MessageId = msg.MessageId,
                        CorrelationId = msg.CorrelationId,
                        StartUtc = nodeStartUtc,
                        EndUtc = nodeEndUtc,
                        Status = ExecutionStatus.Failed,
                        Error = ex.Message,
                        ParentTraceId = options.ParentTraceId
                    };

                    traces.Add(trace);
                    _tracer.RecordTrace(trace);
                    nodesFailed++;

                    _logger.LogError(ex, "Node {NodeId} ({NodeType}) execution failed", nodeId, node.Definition.Type);

                    if (options.StopOnError)
                    {
                        error = ex.Message;
                        status = ExecutionStatus.Failed;
                        break;
                    }

                    // Route to error port if connected
                    var errorConnections = flow.Connections[nodeId]
                        .Where(c => c.SourcePort == "error")
                        .ToList();

                    if (errorConnections.Any())
                    {
                        var errorMsg = msg.Derive(
                            _timeProvider.UtcNow,
                            MessageEnvelope.CreatePayload(new { error = ex.Message, stack = ex.StackTrace }),
                            sourceNodeId: nodeId,
                            sourcePort: "error");

                        foreach (var conn in errorConnections)
                        {
                            messageQueue.Enqueue((conn.TargetNodeId, conn.TargetPort, errorMsg));
                        }
                    }
                }
            }

            if (messagesProcessed >= options.MaxMessages)
            {
                _logger.LogWarning("Flow execution reached message limit ({MaxMessages})", options.MaxMessages);
                status = ExecutionStatus.Failed;
                error = $"Message limit reached ({options.MaxMessages})";
            }
        }
        catch (OperationCanceledException)
        {
            status = ExecutionStatus.Timeout;
            error = "Flow execution timed out";
        }
        catch (Exception ex)
        {
            status = ExecutionStatus.Failed;
            error = ex.Message;
            _logger.LogError(ex, "Flow execution failed");
        }

        return new FlowExecutionResult
        {
            RunId = runId,
            FlowId = flow.Definition.Id,
            StartUtc = startUtc,
            EndUtc = _timeProvider.UtcNow,
            Status = status,
            Traces = traces,
            MessagesProcessed = messagesProcessed,
            NodesSucceeded = nodesSucceeded,
            NodesFailed = nodesFailed,
            NodesSkipped = nodesSkipped,
            Error = error
        };
    }
}

/// <summary>
/// Message emitter implementation.
/// </summary>
internal sealed class MessageEmitter : IMessageEmitter
{
    private readonly ITimeProvider _timeProvider;
    private readonly string _nodeId;
    private readonly IReadOnlyList<WireConnection> _connections;
    private readonly List<EmittedMessage> _emittedMessages = new();

    public IReadOnlyList<EmittedMessage> EmittedMessages => _emittedMessages;

    public MessageEmitter(ITimeProvider timeProvider, string nodeId, IReadOnlyList<WireConnection> connections)
    {
        _timeProvider = timeProvider;
        _nodeId = nodeId;
        _connections = connections;
    }

    public void Emit(string portName, MessageEnvelope message)
    {
        var msgWithSource = message with
        {
            SourceNodeId = _nodeId,
            SourcePort = portName
        };

        foreach (var conn in _connections.Where(c => c.SourcePort == portName))
        {
            _emittedMessages.Add(new EmittedMessage
            {
                TargetNodeId = conn.TargetNodeId,
                TargetPort = conn.TargetPort,
                Message = msgWithSource
            });
        }
    }

    public void EmitError(Exception error, MessageEnvelope originalMessage)
    {
        var errorMsg = originalMessage.Derive(
            _timeProvider.UtcNow,
            MessageEnvelope.CreatePayload(new { error = error.Message, stack = error.StackTrace }),
            sourceNodeId: _nodeId,
            sourcePort: "error");

        Emit("error", errorMsg);
    }
}

internal sealed record EmittedMessage
{
    public required string TargetNodeId { get; init; }
    public required string TargetPort { get; init; }
    public required MessageEnvelope Message { get; init; }
}

/// <summary>
/// Node logger implementation.
/// </summary>
internal sealed class NodeLogger : INodeLogger
{
    private readonly ILogger _logger;
    private readonly string _nodeName;
    private readonly string _nodeId;

    public NodeLogger(ILogger logger, string nodeName, string nodeId)
    {
        _logger = logger;
        _nodeName = nodeName;
        _nodeId = nodeId;
    }

    public void Debug(string message) => _logger.LogDebug("[{NodeName}] {Message}", _nodeName, message);
    public void Info(string message) => _logger.LogInformation("[{NodeName}] {Message}", _nodeName, message);
    public void Warn(string message) => _logger.LogWarning("[{NodeName}] {Message}", _nodeName, message);
    public void Error(string message, Exception? exception = null) => 
        _logger.LogError(exception, "[{NodeName}] {Message}", _nodeName, message);
}

/// <summary>
/// In-memory execution tracer.
/// </summary>
public sealed class InMemoryExecutionTracer : IExecutionTracer
{
    private readonly List<NodeExecutionResult> _traces = new();
    private readonly Dictionary<string, List<NodeExecutionResult>> _tracesByRunId = new();
    private readonly object _lock = new();

    public void RecordTrace(NodeExecutionResult trace)
    {
        lock (_lock)
        {
            _traces.Add(trace);
            
            // Index by runId for O(1) lookup
            if (!_tracesByRunId.TryGetValue(trace.RunId, out var runTraces))
            {
                runTraces = new List<NodeExecutionResult>();
                _tracesByRunId[trace.RunId] = runTraces;
            }
            runTraces.Add(trace);
        }
    }

    public IReadOnlyList<NodeExecutionResult> GetTraces(string runId)
    {
        lock (_lock)
        {
            return _tracesByRunId.TryGetValue(runId, out var traces) 
                ? traces.ToList() 
                : Array.Empty<NodeExecutionResult>();
        }
    }

    public IReadOnlyList<NodeExecutionResult> GetAllTraces()
    {
        lock (_lock)
        {
            return _traces.ToList();
        }
    }

    public void ClearOldTraces(DateTime beforeUtc)
    {
        lock (_lock)
        {
            _traces.RemoveAll(t => t.EndUtc < beforeUtc);
            
            // Also clear from index
            var emptyRunIds = new List<string>();
            foreach (var kvp in _tracesByRunId)
            {
                kvp.Value.RemoveAll(t => t.EndUtc < beforeUtc);
                if (kvp.Value.Count == 0)
                    emptyRunIds.Add(kvp.Key);
            }
            foreach (var runId in emptyRunIds)
                _tracesByRunId.Remove(runId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _traces.Clear();
            _tracesByRunId.Clear();
        }
    }
}
