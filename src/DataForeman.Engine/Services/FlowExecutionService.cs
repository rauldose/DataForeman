using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Engine.Nodes;
using DataForeman.Engine.Runtime;
using DataForeman.Engine.Runtime.Nodes;
using DataForeman.Shared.Definition;
using DataForeman.Shared.Models;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Services;

/// <summary>
/// Service responsible for executing flows, including MQTT-triggered flows.
/// Connects MqttFlowTriggerService events to actual flow execution and
/// handles MQTT publishing from mqtt-out nodes.
/// </summary>
public class FlowExecutionService : IAsyncDisposable
{
    private readonly ILogger<FlowExecutionService> _logger;
    private readonly ConfigService _configService;
    private readonly MqttFlowTriggerService _mqttFlowTriggerService;
    private readonly MqttPublisher _mqttPublisher;
    
    // Flow infrastructure
    private readonly NodeRegistry _nodeRegistry;
    private readonly RegistryRuntimeFactory _runtimeFactory;
    private readonly FlowCompiler _flowCompiler;
    private readonly FlowExecutor _flowExecutor;
    private readonly InMemoryExecutionTracer _tracer;
    
    // Compiled flows cache: FlowId -> CompiledFlow
    private readonly ConcurrentDictionary<string, CompiledFlow> _compiledFlows = new();

    public FlowExecutionService(
        ILogger<FlowExecutionService> logger,
        ConfigService configService,
        MqttFlowTriggerService mqttFlowTriggerService,
        MqttPublisher mqttPublisher)
    {
        _logger = logger;
        _configService = configService;
        _mqttFlowTriggerService = mqttFlowTriggerService;
        _mqttPublisher = mqttPublisher;
        
        // Initialize flow infrastructure
        _nodeRegistry = new NodeRegistry();
        _runtimeFactory = new RegistryRuntimeFactory(_nodeRegistry);
        _flowCompiler = new FlowCompiler();
        _tracer = new InMemoryExecutionTracer();
        
        // Create implementations for node context services
        var timeProvider = new SystemTimeProvider();
        var historian = new NoOpHistorianWriter();
        var tagReader = new NoOpTagValueReader();
        var tagWriter = new NoOpTagValueWriter();
        var nodeMqttPublisher = new MqttPublisherAdapter(mqttPublisher);
        
        _flowExecutor = new FlowExecutor(
            timeProvider,
            _tracer,
            historian,
            tagReader,
            tagWriter,
            logger as ILogger<FlowExecutor> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FlowExecutor>.Instance,
            nodeMqttPublisher);
        
        RegisterNodeTypes();
    }

    /// <summary>
    /// Starts the flow execution service.
    /// </summary>
    public Task StartAsync()
    {
        _logger.LogInformation("Starting flow execution service");
        
        // Subscribe to MQTT flow triggers
        _mqttFlowTriggerService.OnFlowTriggered += HandleFlowTriggered;
        
        // Compile all enabled flows
        CompileAllFlows();
        
        _logger.LogInformation("Flow execution service started with {FlowCount} compiled flows", _compiledFlows.Count);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Refreshes compiled flows when configuration changes.
    /// </summary>
    public void RefreshFlows()
    {
        _logger.LogInformation("Refreshing compiled flows");
        CompileAllFlows();
    }

    /// <summary>
    /// Registers all known node types.
    /// </summary>
    private void RegisterNodeTypes()
    {
        // MQTT nodes (matching UI naming)
        _nodeRegistry.Register(CreateMqttInDescriptor(), () => new MqttInNode());
        _nodeRegistry.Register(CreateMqttOutDescriptor(), () => new MqttOutNode());
        
        // Built-in nodes (using runtime descriptors with dot notation)
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
        
        // Also register with UI naming convention (dashes instead of dots)
        // This ensures flows created in the UI work correctly
        RegisterWithAlternateName("trigger-manual", ManualTriggerRuntime.Descriptor, () => new ManualTriggerRuntime());
        RegisterWithAlternateName("trigger-schedule", TimerTriggerRuntime.Descriptor, () => new TimerTriggerRuntime());
        RegisterWithAlternateName("output-log", DebugRuntime.Descriptor, () => new DebugRuntime());
        RegisterWithAlternateName("logic-compare", CompareRuntime.Descriptor, () => new CompareRuntime());
        RegisterWithAlternateName("math-add", MathAddRuntime.Descriptor, () => new MathAddRuntime());
        RegisterWithAlternateName("math-multiply", MathMultiplyRuntime.Descriptor, () => new MathMultiplyRuntime());
        RegisterWithAlternateName("math-scale", ScaleRuntime.Descriptor, () => new ScaleRuntime());
        RegisterWithAlternateName("tag-input", TagInputRuntime.Descriptor, () => new TagInputRuntime());
        RegisterWithAlternateName("tag-output", TagOutputRuntime.Descriptor, () => new TagOutputRuntime());
        RegisterWithAlternateName("subflow-input", SubflowInputRuntime.Descriptor, () => new SubflowInputRuntime());
        RegisterWithAlternateName("subflow-output", SubflowOutputRuntime.Descriptor, () => new SubflowOutputRuntime());
        
        _logger.LogInformation("Registered {Count} node type mappings (13 base types + 11 UI aliases)", 
            _nodeRegistry.GetAllDescriptors().Count);
    }

    /// <summary>
    /// Registers a node type with an alternate name (for UI compatibility).
    /// Creates a copy of the descriptor with a different type name.
    /// Note: If NodeDescriptor gains new properties, update this method accordingly.
    /// </summary>
    private void RegisterWithAlternateName(string alternateName, NodeDescriptor baseDescriptor, Func<INodeRuntime> runtimeFactory)
    {
        var altDescriptor = new NodeDescriptor
        {
            Type = alternateName,
            DisplayName = baseDescriptor.DisplayName,
            Category = baseDescriptor.Category,
            Description = baseDescriptor.Description,
            Icon = baseDescriptor.Icon,
            Color = baseDescriptor.Color,
            InputPorts = baseDescriptor.InputPorts,
            OutputPorts = baseDescriptor.OutputPorts,
            ConfigSchema = baseDescriptor.ConfigSchema,
            IsFlowNode = baseDescriptor.IsFlowNode,
            IsTrigger = baseDescriptor.IsTrigger
        };
        _nodeRegistry.Register(altDescriptor, runtimeFactory);
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
        
        var enabledFlows = _configService.Flows.Where(f => f.Enabled).ToList();
        _logger.LogInformation("Compiling {Count} enabled flows out of {Total} total flows", 
            enabledFlows.Count, _configService.Flows.Count);
        
        foreach (var flowConfig in enabledFlows)
        {
            try
            {
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
        _logger.LogInformation(
            "Executing flow '{FlowId}' triggered by MQTT message on topic '{Topic}'",
            flowId, topic);

        if (!_compiledFlows.TryGetValue(flowId, out var compiledFlow))
        {
            _logger.LogWarning("Flow '{FlowId}' not found in compiled flows", flowId);
            return;
        }

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
/// No-op tag value reader for flows that don't need tag access.
/// </summary>
internal sealed class NoOpTagValueReader : ITagValueReader
{
    public ValueTask<TagValue?> GetValueAsync(string tagPath, CancellationToken ct)
        => ValueTask.FromResult<TagValue?>(null);
}

/// <summary>
/// No-op tag value writer for flows that don't need tag access.
/// </summary>
internal sealed class NoOpTagValueWriter : ITagValueWriter
{
    public ValueTask WriteValueAsync(string tagPath, object value, CancellationToken ct)
        => ValueTask.CompletedTask;
}
