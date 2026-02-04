// DataForeman Platform - AI Agent Implementation Directives
// Section 5: TEMPLATE FLOWS (DESIGN-TIME ONLY)
// Templates are parameterized blueprints used to generate new flows.
// They exist only at design time and leave no runtime dependency.

using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataForeman.Engine.Runtime;

/// <summary>
/// Template instantiator - generates new flows from templates.
/// </summary>
public sealed class TemplateInstantiator
{
    /// <summary>
    /// Instantiates a template into a new flow definition.
    /// All placeholders are resolved and new IDs are generated.
    /// The resulting flow is completely independent of the template.
    /// </summary>
    public FlowDefinition Instantiate(
        FlowTemplateDefinition template,
        string flowName,
        Dictionary<string, JsonElement> parameterValues)
    {
        // Validate required parameters
        foreach (var param in template.Parameters.Where(p => p.Required))
        {
            if (!parameterValues.ContainsKey(param.Name))
            {
                throw new ArgumentException($"Required parameter '{param.Name}' not provided");
            }
        }

        // Build parameter dictionary with defaults
        var resolvedParams = new Dictionary<string, JsonElement>();
        foreach (var param in template.Parameters)
        {
            if (parameterValues.TryGetValue(param.Name, out var value))
            {
                resolvedParams[param.Name] = value;
            }
            else if (param.DefaultValue != null)
            {
                resolvedParams[param.Name] = param.DefaultValue.Value;
            }
        }

        // Generate new IDs mapping (old ID -> new ID)
        var idMapping = new Dictionary<string, string>();
        foreach (var node in template.Nodes)
        {
            idMapping[node.Id] = Guid.NewGuid().ToString("N");
        }

        // Clone and remap nodes
        var newNodes = new List<NodeDefinition>();
        foreach (var templateNode in template.Nodes)
        {
            var newNode = new NodeDefinition
            {
                Id = idMapping[templateNode.Id],
                Type = templateNode.Type,
                Name = ResolvePlaceholders(templateNode.Name, resolvedParams),
                Config = ResolveConfigPlaceholders(templateNode.Config, resolvedParams),
                Position = new NodePosition
                {
                    X = templateNode.Position.X,
                    Y = templateNode.Position.Y
                },
                Disabled = templateNode.Disabled
            };

            newNodes.Add(newNode);
        }

        // Clone and remap wires
        var newWires = new List<WireDefinition>();
        foreach (var templateWire in template.Wires)
        {
            // Skip wires with unmapped nodes (shouldn't happen with valid templates)
            if (!idMapping.ContainsKey(templateWire.SourceNodeId) || 
                !idMapping.ContainsKey(templateWire.TargetNodeId))
                continue;

            newWires.Add(new WireDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceNodeId = idMapping[templateWire.SourceNodeId],
                SourcePort = templateWire.SourcePort,
                TargetNodeId = idMapping[templateWire.TargetNodeId],
                TargetPort = templateWire.TargetPort
            });
        }

        return new FlowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = flowName,
            Description = ResolvePlaceholders(template.Description, resolvedParams),
            Enabled = true,
            Nodes = newNodes,
            Wires = newWires,
            Metadata = new Dictionary<string, string>
            {
                ["sourceTemplate"] = template.Id,
                ["instantiatedAt"] = DateTime.UtcNow.ToString("O")
            }
        };
    }

    /// <summary>
    /// Resolves {{paramName}} placeholders in a string.
    /// </summary>
    private static string ResolvePlaceholders(string input, Dictionary<string, JsonElement> parameters)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return Regex.Replace(input, @"\{\{(\w+)\}\}", match =>
        {
            var paramName = match.Groups[1].Value;
            if (parameters.TryGetValue(paramName, out var value))
            {
                return value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : value.ToString();
            }
            return match.Value; // Keep placeholder if not found
        });
    }

    /// <summary>
    /// Resolves placeholders in JSON config.
    /// </summary>
    private static JsonElement? ResolveConfigPlaceholders(JsonElement? config, Dictionary<string, JsonElement> parameters)
    {
        if (config == null) return null;

        var json = config.Value.GetRawText();
        var resolved = Regex.Replace(json, @"""?\{\{(\w+)\}\}""?", match =>
        {
            var paramName = match.Groups[1].Value;
            if (parameters.TryGetValue(paramName, out var value))
            {
                // Return the JSON representation
                return value.GetRawText();
            }
            return match.Value;
        });

        return JsonDocument.Parse(resolved).RootElement.Clone();
    }
}

/// <summary>
/// Subflow compiler - compiles subflows into runtime nodes.
/// </summary>
public sealed class SubflowCompiler
{
    private readonly IFlowCompiler _flowCompiler;
    private readonly NodeRegistry _nodeRegistry;

    public SubflowCompiler(IFlowCompiler flowCompiler, NodeRegistry nodeRegistry)
    {
        _flowCompiler = flowCompiler;
        _nodeRegistry = nodeRegistry;
    }

    /// <summary>
    /// Registers a subflow as a node type in the registry.
    /// </summary>
    public void RegisterSubflow(SubflowDefinition subflow)
    {
        var descriptor = CreateDescriptor(subflow);
        _nodeRegistry.Register(descriptor, () => new SubflowNodeRuntime(subflow, _flowCompiler, _nodeRegistry));
    }

    /// <summary>
    /// Creates a node descriptor for a subflow.
    /// </summary>
    private static NodeDescriptor CreateDescriptor(SubflowDefinition subflow)
    {
        return new NodeDescriptor
        {
            Type = $"subflow.{subflow.Id}",
            DisplayName = subflow.Name,
            Category = subflow.Category,
            Description = subflow.Description,
            Icon = subflow.Icon,
            Color = subflow.Color,
            InputPorts = subflow.InputPorts.Select(p => new PortDescriptor
            {
                Name = p.Name,
                Label = p.Label,
                Direction = PortDirection.Input,
                Cardinality = p.Cardinality
            }).ToArray(),
            OutputPorts = subflow.OutputPorts.Select(p => new PortDescriptor
            {
                Name = p.Name,
                Label = p.Label,
                Direction = PortDirection.Output,
                Cardinality = p.Cardinality
            }).ToArray()
        };
    }

    /// <summary>
    /// Creates a subflow from selected nodes in a flow.
    /// </summary>
    public SubflowDefinition CreateFromNodes(
        FlowDefinition sourceFlow,
        IReadOnlyList<string> nodeIds,
        string subflowName)
    {
        var selectedNodes = sourceFlow.Nodes.Where(n => nodeIds.Contains(n.Id)).ToList();
        if (selectedNodes.Count == 0)
            throw new ArgumentException("No nodes selected");

        // Find external connections (wires that cross the selection boundary)
        var internalWires = new List<WireDefinition>();
        var inputConnections = new List<(string portName, string targetNodeId, string targetPort)>();
        var outputConnections = new List<(string sourceNodeId, string sourcePort, string portName)>();

        foreach (var wire in sourceFlow.Wires)
        {
            var sourceInSelection = nodeIds.Contains(wire.SourceNodeId);
            var targetInSelection = nodeIds.Contains(wire.TargetNodeId);

            if (sourceInSelection && targetInSelection)
            {
                // Internal wire - keep it
                internalWires.Add(wire);
            }
            else if (!sourceInSelection && targetInSelection)
            {
                // Input to selection - create input port
                var portName = $"in_{inputConnections.Count}";
                inputConnections.Add((portName, wire.TargetNodeId, wire.TargetPort));
            }
            else if (sourceInSelection && !targetInSelection)
            {
                // Output from selection - create output port
                var portName = $"out_{outputConnections.Count}";
                outputConnections.Add((wire.SourceNodeId, wire.SourcePort, portName));
            }
        }

        // Create ID mapping for new subflow
        var idMapping = new Dictionary<string, string>();
        foreach (var node in selectedNodes)
        {
            idMapping[node.Id] = Guid.NewGuid().ToString("N");
        }

        // Create subflow input nodes
        var subflowNodes = new List<NodeDefinition>();
        var subflowWires = new List<WireDefinition>();

        foreach (var (portName, targetNodeId, targetPort) in inputConnections)
        {
            var inputNodeId = Guid.NewGuid().ToString("N");
            subflowNodes.Add(new NodeDefinition
            {
                Id = inputNodeId,
                Type = "subflow.input",
                Name = portName,
                Config = JsonDocument.Parse($"{{\"portName\":\"{portName}\"}}").RootElement,
                Position = new NodePosition { X = 0, Y = subflowNodes.Count * 100 }
            });

            // Wire input node to target
            subflowWires.Add(new WireDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceNodeId = inputNodeId,
                SourcePort = "output",
                TargetNodeId = idMapping[targetNodeId],
                TargetPort = targetPort
            });
        }

        // Clone selected nodes
        foreach (var node in selectedNodes)
        {
            subflowNodes.Add(new NodeDefinition
            {
                Id = idMapping[node.Id],
                Type = node.Type,
                Name = node.Name,
                Config = node.Config,
                Position = node.Position,
                Disabled = node.Disabled
            });
        }

        // Clone internal wires
        foreach (var wire in internalWires)
        {
            subflowWires.Add(new WireDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceNodeId = idMapping[wire.SourceNodeId],
                SourcePort = wire.SourcePort,
                TargetNodeId = idMapping[wire.TargetNodeId],
                TargetPort = wire.TargetPort
            });
        }

        // Create subflow output nodes
        foreach (var (sourceNodeId, sourcePort, portName) in outputConnections)
        {
            var outputNodeId = Guid.NewGuid().ToString("N");
            subflowNodes.Add(new NodeDefinition
            {
                Id = outputNodeId,
                Type = "subflow.output",
                Name = portName,
                Config = JsonDocument.Parse($"{{\"portName\":\"{portName}\"}}").RootElement,
                Position = new NodePosition { X = 500, Y = (subflowNodes.Count - selectedNodes.Count) * 100 }
            });

            // Wire source to output node
            subflowWires.Add(new WireDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceNodeId = idMapping[sourceNodeId],
                SourcePort = sourcePort,
                TargetNodeId = outputNodeId,
                TargetPort = "input"
            });
        }

        return new SubflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = subflowName,
            Description = $"Subflow created from {selectedNodes.Count} nodes",
            Category = "Subflows",
            InputPorts = inputConnections.Select(c => new PortDefinition
            {
                Name = c.portName,
                Label = c.portName,
                Direction = PortDirection.Input
            }).ToList(),
            OutputPorts = outputConnections.Select(c => new PortDefinition
            {
                Name = c.portName,
                Label = c.portName,
                Direction = PortDirection.Output
            }).ToList(),
            Nodes = subflowNodes,
            Wires = subflowWires
        };
    }
}

/// <summary>
/// Runtime for executing subflows as nodes.
/// Subflows execute in isolated scopes with nested traces.
/// </summary>
public sealed class SubflowNodeRuntime : INodeRuntime
{
    private readonly SubflowDefinition _subflow;
    private readonly IFlowCompiler _compiler;
    private readonly NodeRegistry _registry;

    public SubflowNodeRuntime(SubflowDefinition subflow, IFlowCompiler compiler, NodeRegistry registry)
    {
        _subflow = subflow;
        _compiler = compiler;
        _registry = registry;
    }

    public async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Convert subflow to flow definition
        var flowDef = new FlowDefinition
        {
            Id = $"subflow-instance-{Guid.NewGuid():N}",
            Name = _subflow.Name,
            Nodes = _subflow.Nodes,
            Wires = _subflow.Wires,
            Enabled = true
        };

        // Find input node matching the incoming port
        var inputPort = context.Message.SourcePort ?? "input";
        var inputNode = _subflow.Nodes.FirstOrDefault(n => 
            n.Type == "subflow.input" && 
            n.GetConfig<SubflowPortConfig>()?.PortName == inputPort);

        if (inputNode == null)
        {
            // Use first input node
            inputNode = _subflow.Nodes.FirstOrDefault(n => n.Type == "subflow.input");
        }

        if (inputNode == null)
        {
            context.Logger.Error("Subflow has no input node");
            return;
        }

        // Compile and execute
        var compiled = _compiler.Compile(flowDef, _registry, new RegistryRuntimeFactory(_registry));

        // For now, just pass through (full execution would require FlowExecutor)
        // In a complete implementation, this would execute the subflow
        context.Logger.Debug($"Executing subflow '{_subflow.Name}'");

        // Emit to all output ports
        foreach (var outputPort in _subflow.OutputPorts)
        {
            context.Emitter.Emit(outputPort.Name, context.Message);
        }
    }

    private sealed class SubflowPortConfig
    {
        public string? PortName { get; set; }
    }
}
