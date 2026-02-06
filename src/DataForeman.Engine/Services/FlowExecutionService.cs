using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DataForeman.Engine.Nodes;
using DataForeman.Engine.Runtime;
using DataForeman.Engine.Runtime.Nodes;
using DataForeman.Shared.Definition;
using DataForeman.Shared.Models;
using DataForeman.Shared.Mqtt;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Services;

/// <summary>
/// Allows external services (e.g. state machines) to trigger flow execution by ID.
/// </summary>
public interface IFlowRunner
{
    /// <summary>Triggers execution of a compiled flow.  Returns true if the flow was found and started.</summary>
    Task<bool> TriggerFlowAsync(string flowId, string triggerSource);
}

/// <summary>
/// Service responsible for executing flows, including MQTT-triggered flows.
/// Connects MqttFlowTriggerService events to actual flow execution and
/// handles MQTT publishing from mqtt-out nodes.
/// </summary>
public class FlowExecutionService : IFlowRunner, IAsyncDisposable
{
    private readonly ILogger<FlowExecutionService> _logger;
    private readonly ConfigService _configService;
    private readonly MqttFlowTriggerService _mqttFlowTriggerService;
    private readonly MqttPublisher _mqttPublisher;
    private readonly InternalTagStore _internalTagStore;
    private readonly CSharpScriptService _scriptService;
    
    // Flow infrastructure
    private readonly NodeRegistry _nodeRegistry;
    private readonly RegistryRuntimeFactory _runtimeFactory;
    private readonly FlowCompiler _flowCompiler;
    private readonly FlowExecutor _flowExecutor;
    private readonly MqttExecutionTracer _tracer;
    
    // Compiled flows cache: FlowId -> CompiledFlow
    private readonly ConcurrentDictionary<string, CompiledFlow> _compiledFlows = new();
    // FlowId -> Name lookup built during compilation to avoid repeated list scans
    private readonly ConcurrentDictionary<string, string> _flowNames = new();

    public FlowExecutionService(
        ILogger<FlowExecutionService> logger,
        ConfigService configService,
        MqttFlowTriggerService mqttFlowTriggerService,
        MqttPublisher mqttPublisher,
        InternalTagStore internalTagStore,
        ILoggerFactory loggerFactory,
        PollEngineTagAdapter tagAdapter,
        CSharpScriptService scriptService)
    {
        _logger = logger;
        _configService = configService;
        _mqttFlowTriggerService = mqttFlowTriggerService;
        _mqttPublisher = mqttPublisher;
        _internalTagStore = internalTagStore;
        _scriptService = scriptService;
        
        // Initialize flow infrastructure
        _nodeRegistry = new NodeRegistry();
        _runtimeFactory = new RegistryRuntimeFactory(_nodeRegistry);
        _flowCompiler = new FlowCompiler();
        _tracer = new MqttExecutionTracer(mqttPublisher, loggerFactory.CreateLogger<MqttExecutionTracer>());
        
        // Create implementations for node context services
        var timeProvider = new SystemTimeProvider();
        var historian = new NoOpHistorianWriter();
        var tagReader = new PollEngineTagValueReader(tagAdapter);
        var tagWriter = new PollEngineTagValueWriter(tagAdapter);
        var nodeMqttPublisher = new MqttPublisherAdapter(mqttPublisher);
        var contextStore = new ContextStoreAdapter(internalTagStore);
        
        _flowExecutor = new FlowExecutor(
            timeProvider,
            _tracer,
            historian,
            tagReader,
            tagWriter,
            logger as ILogger<FlowExecutor> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FlowExecutor>.Instance,
            nodeMqttPublisher,
            contextStore);
        
        RegisterNodeTypes();
    }

    /// <summary>
    /// Starts the flow execution service.
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting flow execution service");
        
        // Subscribe to MQTT flow triggers
        _mqttFlowTriggerService.OnFlowTriggered += HandleFlowTriggered;
        
        // Compile all enabled flows
        CompileAllFlows();
        
        // Publish initial deployment statuses (awaited, not fire-and-forget)
        await PublishAllDeploymentStatusesAsync();
        
        _logger.LogInformation("Flow execution service started with {FlowCount} compiled flows", _compiledFlows.Count);
    }

    /// <summary>
    /// Refreshes compiled flows when configuration changes and publishes deployment status.
    /// </summary>
    public async Task RefreshFlowsAsync()
    {
        _logger.LogInformation("Refreshing compiled flows");
        CompileAllFlows();
        await PublishAllDeploymentStatusesAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> TriggerFlowAsync(string flowId, string triggerSource)
    {
        if (!_compiledFlows.TryGetValue(flowId, out var compiled))
        {
            _logger.LogWarning("TriggerFlowAsync: flow '{FlowId}' not found in compiled flows", flowId);
            return false;
        }

        _flowNames.TryGetValue(flowId, out var flowName);
        _logger.LogInformation("Flow '{FlowName}' triggered by {Source}", flowName ?? flowId, triggerSource);

        // Find the first trigger node as the entry point,
        // or fall back to the first node if none found.
        var triggerNodeId = compiled.TriggerNodes.FirstOrDefault()?.Definition.Id
            ?? compiled.Nodes.Keys.FirstOrDefault();

        if (triggerNodeId == null)
        {
            _logger.LogWarning("Flow '{FlowId}' has no nodes to execute", flowId);
            return false;
        }

        var initialMsg = MessageEnvelope.Create(
            createdUtc: DateTime.UtcNow,
            payload: System.Text.Json.JsonSerializer.SerializeToElement(new { source = triggerSource }),
            correlationId: Guid.NewGuid().ToString("N"));

        var opts = new FlowExecutionOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxMessages = 100,
            StopOnError = false
        };

        var result = await _flowExecutor.ExecuteAsync(compiled, triggerNodeId, initialMsg, opts, CancellationToken.None);

        if (result.Status == ExecutionStatus.Success)
            _logger.LogInformation("Flow '{FlowName}' completed OK ({Nodes} nodes)", flowName ?? flowId, result.NodesSucceeded);
        else
            _logger.LogWarning("Flow '{FlowName}' ended with {Status}: {Error}", flowName ?? flowId, result.Status, result.Error);

        // Publish summary so the UI gets notified
        var summary = new FlowRunSummaryMessage
        {
            FlowId = flowId,
            FlowName = flowName ?? flowId,
            TriggerNodeId = triggerNodeId,
            TriggerTopic = triggerSource,
            Outcome = result.Status.ToString(),
            NodesExecuted = result.NodesSucceeded,
            MessagesHandled = result.MessagesProcessed,
            DurationMs = (DateTime.UtcNow - initialMsg.CreatedUtc).TotalMilliseconds,
            StartedUtc = initialMsg.CreatedUtc,
            CompletedUtc = DateTime.UtcNow
        };
        await _mqttPublisher.PublishFlowRunSummaryAsync(summary);

        return result.Status == ExecutionStatus.Success;
    }

    /// <summary>
    /// Registers all known node types.
    /// </summary>
    private void RegisterNodeTypes()
    {
        // MQTT nodes
        _nodeRegistry.Register(CreateMqttInDescriptor(), () => new MqttInNode());
        _nodeRegistry.Register(CreateMqttOutDescriptor(), () => new MqttOutNode());
        
        // Context nodes (internal tags)
        _nodeRegistry.Register(ContextGetNode.Descriptor, () => new ContextGetNode());
        _nodeRegistry.Register(ContextSetNode.Descriptor, () => new ContextSetNode());
        
        // Built-in nodes (all using dash-separated naming convention to match UI)
        _nodeRegistry.Register(ManualTriggerRuntime.Descriptor, () => new ManualTriggerRuntime());
        _nodeRegistry.Register(TimerTriggerRuntime.Descriptor, () => new TimerTriggerRuntime());
        _nodeRegistry.Register(DebugRuntime.Descriptor, () => new DebugRuntime());
        _nodeRegistry.Register(CompareRuntime.Descriptor, () => new CompareRuntime());
        _nodeRegistry.Register(MathAddRuntime.Descriptor, () => new MathAddRuntime());
        _nodeRegistry.Register(MathMultiplyRuntime.Descriptor, () => new MathMultiplyRuntime());
        _nodeRegistry.Register(ScaleRuntime.Descriptor, () => new ScaleRuntime());
        _nodeRegistry.Register(TagInputRuntime.Descriptor, () => new TagInputRuntime());
        _nodeRegistry.Register(TagOutputRuntime.Descriptor, () => new TagOutputRuntime());
        _nodeRegistry.Register(SubflowInputRuntime.Descriptor, () => new SubflowInputRuntime());
        _nodeRegistry.Register(SubflowOutputRuntime.Descriptor, () => new SubflowOutputRuntime());

        // Extended nodes — output
        _nodeRegistry.Register(NotificationRuntime.Descriptor, () => new NotificationRuntime());

        // Extended nodes — math
        _nodeRegistry.Register(MathSubtractRuntime.Descriptor, () => new MathSubtractRuntime());
        _nodeRegistry.Register(MathDivideRuntime.Descriptor, () => new MathDivideRuntime());

        // Extended nodes — logic
        _nodeRegistry.Register(BranchRuntime.Descriptor, () => new BranchRuntime());
        _nodeRegistry.Register(LogicAndRuntime.Descriptor, () => new LogicAndRuntime());
        _nodeRegistry.Register(LogicOrRuntime.Descriptor, () => new LogicOrRuntime());

        // Extended nodes — utility
        _nodeRegistry.Register(DelayRuntime.Descriptor, () => new DelayRuntime());
        _nodeRegistry.Register(FilterRuntime.Descriptor, () => new FilterRuntime());
        _nodeRegistry.Register(ConstantRuntime.Descriptor, () => new ConstantRuntime());

        // Extended nodes — data processing
        _nodeRegistry.Register(SmoothRuntime.Descriptor, () => new SmoothRuntime());
        _nodeRegistry.Register(AggregateRuntime.Descriptor, () => new AggregateRuntime());
        _nodeRegistry.Register(DeadbandRuntime.Descriptor, () => new DeadbandRuntime());
        _nodeRegistry.Register(RateOfChangeRuntime.Descriptor, () => new RateOfChangeRuntime());

        // Extended nodes — function
        _nodeRegistry.Register(SwitchRuntime.Descriptor, () => new SwitchRuntime());
        _nodeRegistry.Register(TemplateRuntime.Descriptor, () => new TemplateRuntime());

        // Extended nodes — triggers
        _nodeRegistry.Register(TagTriggerRuntime.Descriptor, () => new TagTriggerRuntime());
        _nodeRegistry.Register(InjectTimerRuntime.Descriptor, () => new InjectTimerRuntime());

        // Extended nodes — communication
        _nodeRegistry.Register(HttpRequestRuntime.Descriptor, () => new HttpRequestRuntime());

        // Extended nodes — scripts
        _nodeRegistry.Register(CSharpScriptRuntime.Descriptor, () => new CSharpScriptRuntime(_scriptService));

        // Extended nodes — integration stubs
        _nodeRegistry.Register(JavaScriptRuntime.Descriptor, () => new JavaScriptRuntime());
        _nodeRegistry.Register(DebugSidebarRuntime.Descriptor, () => new DebugSidebarRuntime());
        _nodeRegistry.Register(LinkInRuntime.Descriptor, () => new LinkInRuntime());
        _nodeRegistry.Register(LinkOutRuntime.Descriptor, () => new LinkOutRuntime());
        _nodeRegistry.Register(StorageFileRuntime.Descriptor, () => new StorageFileRuntime());
        _nodeRegistry.Register(StorageSqliteRuntime.Descriptor, () => new StorageSqliteRuntime());

        _logger.LogInformation("Registered {Count} node types", _nodeRegistry.GetAllDescriptors().Count);
    }

    /// <summary>
    /// Creates the mqtt-in node descriptor.
    /// </summary>
    private static NodeDescriptor CreateMqttInDescriptor()
    {
        return new NodeDescriptor
        {
            Type = "mqtt-in",
            DisplayName = "MQTT Subscribe",
            Category = "Communication",
            Description = "Subscribe to MQTT topic",
            Icon = "fa-arrow-right-to-bracket",
            Color = "#9333ea",
            IsTrigger = true,
            InputPorts = Array.Empty<PortDescriptor>(),
            OutputPorts = new[]
            {
                new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
            },
            ConfigSchema = new NodeConfigSchema
            {
                Properties = new[]
                {
                    new ConfigProperty { Name = "broker", Label = "Broker URL", Type = "string" },
                    new ConfigProperty { Name = "topic", Label = "Topic", Type = "string", Required = true },
                    new ConfigProperty { Name = "qos", Label = "QoS", Type = "number" }
                }
            }
        };
    }

    /// <summary>
    /// Creates the mqtt-out node descriptor.
    /// </summary>
    private static NodeDescriptor CreateMqttOutDescriptor()
    {
        return new NodeDescriptor
        {
            Type = "mqtt-out",
            DisplayName = "MQTT Publish",
            Category = "Communication",
            Description = "Publish to MQTT topic",
            Icon = "fa-arrow-right-from-bracket",
            Color = "#9333ea",
            IsTrigger = false,
            InputPorts = new[]
            {
                new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
            },
            OutputPorts = new[]
            {
                new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
            },
            ConfigSchema = new NodeConfigSchema
            {
                Properties = new[]
                {
                    new ConfigProperty { Name = "broker", Label = "Broker URL", Type = "string" },
                    new ConfigProperty { Name = "topic", Label = "Topic", Type = "string", Required = true },
                    new ConfigProperty { Name = "qos", Label = "QoS", Type = "number" },
                    new ConfigProperty { Name = "retain", Label = "Retain", Type = "boolean" }
                }
            }
        };
    }

    /// <summary>
    /// Compiles all enabled flows from configuration.
    /// </summary>
    private void CompileAllFlows()
    {
        _compiledFlows.Clear();
        _flowNames.Clear();
        
        var enabledFlows = _configService.Flows.Where(f => f.Enabled).ToList();
        _logger.LogInformation("Compiling {Count} enabled flows out of {Total} total flows", 
            enabledFlows.Count, _configService.Flows.Count);
        
        foreach (var flowConfig in enabledFlows)
        {
            try
            {
                // Cache the name for later use in summaries
                _flowNames[flowConfig.Id] = flowConfig.Name;

                // Check if flow has mqtt-in nodes
                var mqttInNodes = flowConfig.Nodes.Where(n => n.Type == "mqtt-in").ToList();
                if (mqttInNodes.Count > 0)
                {
                    _logger.LogInformation("Flow '{FlowName}' has {Count} mqtt-in nodes", 
                        flowConfig.Name, mqttInNodes.Count);
                }

                var flowDefinition = ConvertToFlowDefinition(flowConfig);
                var compiledFlow = _flowCompiler.Compile(flowDefinition, _nodeRegistry, _runtimeFactory);
                _compiledFlows[flowConfig.Id] = compiledFlow;
                
                _logger.LogInformation("Compiled flow '{FlowName}' (id: {FlowId}) with {NodeCount} nodes and {WireCount} wires",
                    flowConfig.Name, flowConfig.Id, flowDefinition.Nodes.Count, flowDefinition.Wires.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile flow '{FlowName}' (id: {FlowId}): {Message}",
                    flowConfig.Name, flowConfig.Id, ex.Message);
            }
        }

        _logger.LogInformation("Compiled {Count} flows out of {Total} total", 
            _compiledFlows.Count, _configService.Flows.Count);
    }

    /// <summary>
    /// Publishes a <see cref="FlowDeploymentStatusMessage"/> for every known flow
    /// so the App can detect drift between its local edits and the running Engine.
    /// </summary>
    private async Task PublishAllDeploymentStatusesAsync()
    {
        foreach (var flow in _configService.Flows)
        {
            try
            {
                var isCompiled = _compiledFlows.ContainsKey(flow.Id);
                var status = new FlowDeploymentStatusMessage
                {
                    FlowId = flow.Id,
                    FlowName = flow.Name,
                    ConfigHash = flow.ComputeContentHash(),
                    IsCompiled = isCompiled,
                    IsEnabled = flow.Enabled,
                    NodeCount = flow.Nodes.Count,
                    CompiledAtUtc = DateTime.UtcNow
                };
                await _mqttPublisher.PublishFlowDeploymentStatusAsync(status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish deployment status for flow '{FlowId}'", flow.Id);
            }
        }
    }

    /// <summary>
    /// Converts FlowConfig to FlowDefinition for runtime execution.
    /// </summary>
    private static FlowDefinition ConvertToFlowDefinition(FlowConfig config)
    {
        var nodes = config.Nodes.Select(n => new NodeDefinition
        {
            Id = n.Id,
            Type = n.Type,
            Name = n.Label,
            Config = n.Properties.Count > 0 
                ? JsonDocument.Parse(JsonSerializer.Serialize(n.Properties)).RootElement.Clone()
                : null,
            Position = new NodePosition { X = n.X, Y = n.Y },
            Disabled = false
        }).ToList();

        var wires = config.Edges.Select(e => new WireDefinition
        {
            Id = e.Id,
            SourceNodeId = e.SourceNodeId,
            SourcePort = NormalizePortName(e.SourcePortId, "output"),
            TargetNodeId = e.TargetNodeId,
            TargetPort = NormalizePortName(e.TargetPortId, "input")
        }).ToList();

        return new FlowDefinition
        {
            Id = config.Id,
            Name = config.Name,
            Description = config.Description ?? string.Empty,
            Enabled = config.Enabled,
            Nodes = nodes,
            Wires = wires
        };
    }

    /// <summary>
    /// Normalizes port IDs like "output-0" to port names like "output".
    /// </summary>
    private static string NormalizePortName(string portId, string defaultName)
    {
        if (string.IsNullOrEmpty(portId))
            return defaultName;
        
        // Handle "output-0", "input-0" style port IDs
        var parts = portId.Split('-');
        if (parts.Length >= 1)
        {
            return parts[0];
        }
        
        return portId;
    }

    /// <summary>
    /// Handles MQTT-triggered flow execution.
    /// </summary>
    private async void HandleFlowTriggered(string flowId, string nodeId, string topic, string payload)
    {
        try
        {
            await ExecuteFlowAsync(flowId, nodeId, topic, payload);
        }
        catch (Exception ex)
        {
            // Catch all exceptions to prevent async void from crashing the application
            _logger.LogError(ex, "Unhandled exception in MQTT flow trigger handler for flow '{FlowId}'", flowId);
        }
    }

    /// <summary>
    /// Executes a flow triggered by an MQTT message.
    /// </summary>
    private async Task ExecuteFlowAsync(string flowId, string nodeId, string topic, string payload)
    {
        var startedUtc = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Executing flow '{FlowId}' triggered by MQTT message on topic '{Topic}'",
            flowId, topic);

        if (!_compiledFlows.TryGetValue(flowId, out var compiledFlow))
        {
            _logger.LogWarning("Flow '{FlowId}' not found in compiled flows", flowId);
            return;
        }

        // Look up the flow name from the cached dictionary
        _flowNames.TryGetValue(flowId, out var flowName);

        // Create initial message with MQTT payload
        JsonElement payloadElement;
        try
        {
            // Try to parse as JSON
            payloadElement = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch
        {
            // If not valid JSON, properly serialize as a JSON string
            payloadElement = JsonSerializer.SerializeToElement(payload);
        }

        var initialMessage = MessageEnvelope.Create(
            createdUtc: DateTime.UtcNow,
            payload: payloadElement,
            correlationId: Guid.NewGuid().ToString("N"));

        // Execute the flow starting from the mqtt-in node
        var options = new FlowExecutionOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxMessages = 100,
            StopOnError = false
        };

        var result = await _flowExecutor.ExecuteAsync(
            compiledFlow,
            nodeId,
            initialMessage,
            options,
            CancellationToken.None);

        sw.Stop();

        if (result.Status == ExecutionStatus.Success)
        {
            _logger.LogInformation(
                "Flow '{FlowId}' execution completed: {NodesSucceeded} nodes succeeded, {MessagesProcessed} messages processed",
                flowId, result.NodesSucceeded, result.MessagesProcessed);
        }
        else
        {
            _logger.LogWarning(
                "Flow '{FlowId}' execution completed with status {Status}: {Error}",
                flowId, result.Status, result.Error);
        }

        // Publish a run summary so the UI knows what happened
        var summary = new FlowRunSummaryMessage
        {
            FlowId = flowId,
            FlowName = flowName ?? flowId,
            TriggerNodeId = nodeId,
            TriggerTopic = topic,
            Outcome = result.Status.ToString(),
            NodesExecuted = result.NodesSucceeded,
            MessagesHandled = result.MessagesProcessed,
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ErrorDetail = result.Error,
            StartedUtc = startedUtc,
            CompletedUtc = DateTime.UtcNow
        };
        await _mqttPublisher.PublishFlowRunSummaryAsync(summary);
    }

    public ValueTask DisposeAsync()
    {
        _mqttFlowTriggerService.OnFlowTriggered -= HandleFlowTriggered;
        _compiledFlows.Clear();
        _logger.LogInformation("Flow execution service disposed");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adapter that wraps MqttPublisher to implement INodeMqttPublisher.
/// </summary>
internal sealed class MqttPublisherAdapter : INodeMqttPublisher
{
    private readonly MqttPublisher _mqttPublisher;

    public MqttPublisherAdapter(MqttPublisher mqttPublisher)
    {
        _mqttPublisher = mqttPublisher;
    }

    public async ValueTask PublishAsync(string topic, string payload, int qos = 0, bool retain = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _mqttPublisher.PublishMessageAsync(topic, payload, qos, retain);
    }
}

/// <summary>
/// No-op historian writer for flows that don't need historian.
/// </summary>
internal sealed class NoOpHistorianWriter : IHistorianWriter
{
    public ValueTask WriteAsync(HistorianMeasurement measurement, CancellationToken ct)
        => ValueTask.CompletedTask;
}

/// <summary>
/// Adapts PollEngineTagAdapter (IStateMachineTagReader) to ITagValueReader for flow nodes.
/// Converts from Drivers.TagValue to Shared.Runtime.TagValue.
/// </summary>
internal sealed class PollEngineTagValueReader : ITagValueReader
{
    private readonly PollEngineTagAdapter _adapter;

    public PollEngineTagValueReader(PollEngineTagAdapter adapter) => _adapter = adapter;

    public ValueTask<TagValue?> GetValueAsync(string tagPath, CancellationToken ct)
    {
        var driverValue = _adapter.GetCurrentTagValue(tagPath);
        if (driverValue == null)
            return ValueTask.FromResult<TagValue?>(null);

        var sharedValue = new TagValue
        {
            TagPath = tagPath,
            Value = driverValue.Value,
            TimestampUtc = driverValue.Timestamp,
            Quality = driverValue.Quality
        };
        return ValueTask.FromResult<TagValue?>(sharedValue);
    }
}

/// <summary>
/// Adapts PollEngineTagAdapter (IStateMachineTagWriter) to ITagValueWriter for flow nodes.
/// </summary>
internal sealed class PollEngineTagValueWriter : ITagValueWriter
{
    private readonly PollEngineTagAdapter _adapter;

    public PollEngineTagValueWriter(PollEngineTagAdapter adapter) => _adapter = adapter;

    public async ValueTask WriteValueAsync(string tagPath, object value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _adapter.WriteTagValueAsync(tagPath, value);
    }
}
