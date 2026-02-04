// DataForeman Platform - AI Agent Implementation Directives
// Section 4.1: GRAPH VALIDATION (COMPILE-TIME)
// Validate before runtime execution. Fail before execution, never during.

using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Runtime;

/// <summary>
/// Default flow validator implementation.
/// </summary>
public sealed class FlowValidator : IFlowValidator
{
    public FlowValidationResult Validate(FlowDefinition flow, INodeRegistry nodeRegistry)
    {
        var errors = new List<FlowValidationError>();
        var warnings = new List<FlowValidationWarning>();

        // Validate all nodes
        var nodeIds = new HashSet<string>();
        foreach (var node in flow.Nodes)
        {
            // Check for duplicate IDs
            if (!nodeIds.Add(node.Id))
            {
                errors.Add(new FlowValidationError
                {
                    Code = "DUPLICATE_NODE_ID",
                    Message = $"Duplicate node ID: {node.Id}",
                    NodeId = node.Id
                });
                continue;
            }

            // Check node type exists
            var descriptor = nodeRegistry.GetDescriptor(node.Type);
            if (descriptor == null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "UNKNOWN_NODE_TYPE",
                    Message = $"Unknown node type: {node.Type}",
                    NodeId = node.Id
                });
                continue;
            }

            // Validate node can be used in flows
            if (!descriptor.IsFlowNode)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "INVALID_NODE_FOR_FLOW",
                    Message = $"Node type '{node.Type}' cannot be used in flows (internal only)",
                    NodeId = node.Id
                });
            }

            // Disabled nodes generate warnings
            if (node.Disabled)
            {
                warnings.Add(new FlowValidationWarning
                {
                    Code = "DISABLED_NODE",
                    Message = $"Node '{node.Name}' ({node.Id}) is disabled and will be skipped",
                    NodeId = node.Id
                });
            }
        }

        // Validate all wires
        var wireIds = new HashSet<string>();
        var inputPortConnections = new Dictionary<string, List<string>>(); // nodeId:port -> wire IDs

        foreach (var wire in flow.Wires)
        {
            // Check for duplicate wire IDs
            if (!wireIds.Add(wire.Id))
            {
                errors.Add(new FlowValidationError
                {
                    Code = "DUPLICATE_WIRE_ID",
                    Message = $"Duplicate wire ID: {wire.Id}",
                    WireId = wire.Id
                });
                continue;
            }

            // Check source node exists
            var sourceNode = flow.Nodes.FirstOrDefault(n => n.Id == wire.SourceNodeId);
            if (sourceNode == null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "DANGLING_WIRE_SOURCE",
                    Message = $"Wire source node not found: {wire.SourceNodeId}",
                    WireId = wire.Id
                });
                continue;
            }

            // Check target node exists
            var targetNode = flow.Nodes.FirstOrDefault(n => n.Id == wire.TargetNodeId);
            if (targetNode == null)
            {
                errors.Add(new FlowValidationError
                {
                    Code = "DANGLING_WIRE_TARGET",
                    Message = $"Wire target node not found: {wire.TargetNodeId}",
                    WireId = wire.Id
                });
                continue;
            }

            // Validate source port exists
            var sourceDescriptor = nodeRegistry.GetDescriptor(sourceNode.Type);
            if (sourceDescriptor != null)
            {
                var sourcePort = sourceDescriptor.OutputPorts.FirstOrDefault(p => p.Name == wire.SourcePort);
                if (sourcePort == null)
                {
                    errors.Add(new FlowValidationError
                    {
                        Code = "INVALID_SOURCE_PORT",
                        Message = $"Source port '{wire.SourcePort}' does not exist on node type '{sourceNode.Type}'",
                        WireId = wire.Id,
                        NodeId = wire.SourceNodeId
                    });
                }
            }

            // Validate target port exists
            var targetDescriptor = nodeRegistry.GetDescriptor(targetNode.Type);
            if (targetDescriptor != null)
            {
                var targetPort = targetDescriptor.InputPorts.FirstOrDefault(p => p.Name == wire.TargetPort);
                if (targetPort == null)
                {
                    errors.Add(new FlowValidationError
                    {
                        Code = "INVALID_TARGET_PORT",
                        Message = $"Target port '{wire.TargetPort}' does not exist on node type '{targetNode.Type}'",
                        WireId = wire.Id,
                        NodeId = wire.TargetNodeId
                    });
                }
                else
                {
                    // Track connections for cardinality validation
                    var key = $"{wire.TargetNodeId}:{wire.TargetPort}";
                    if (!inputPortConnections.ContainsKey(key))
                        inputPortConnections[key] = new List<string>();
                    inputPortConnections[key].Add(wire.Id);

                    // Check cardinality
                    if (targetPort.Cardinality == PortCardinality.Single && inputPortConnections[key].Count > 1)
                    {
                        errors.Add(new FlowValidationError
                        {
                            Code = "PORT_CARDINALITY_EXCEEDED",
                            Message = $"Port '{wire.TargetPort}' on node '{targetNode.Id}' only accepts single connection",
                            WireId = wire.Id,
                            NodeId = wire.TargetNodeId
                        });
                    }
                }
            }
        }

        // Check for required ports that are not connected
        foreach (var node in flow.Nodes.Where(n => !n.Disabled))
        {
            var descriptor = nodeRegistry.GetDescriptor(node.Type);
            if (descriptor == null) continue;

            foreach (var inputPort in descriptor.InputPorts.Where(p => p.Required))
            {
                var key = $"{node.Id}:{inputPort.Name}";
                if (!inputPortConnections.ContainsKey(key))
                {
                    // Only warn for non-trigger nodes (triggers don't need inputs)
                    if (!descriptor.IsTrigger)
                    {
                        warnings.Add(new FlowValidationWarning
                        {
                            Code = "REQUIRED_PORT_UNCONNECTED",
                            Message = $"Required input port '{inputPort.Name}' on node '{node.Name}' ({node.Id}) is not connected",
                            NodeId = node.Id
                        });
                    }
                }
            }
        }

        // Check for at least one trigger node
        var hasTrigger = flow.Nodes.Any(n => 
            !n.Disabled && 
            nodeRegistry.GetDescriptor(n.Type)?.IsTrigger == true);

        if (!hasTrigger && flow.Enabled)
        {
            warnings.Add(new FlowValidationWarning
            {
                Code = "NO_TRIGGER_NODE",
                Message = "Flow has no trigger node and will not execute automatically"
            });
        }

        return new FlowValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}
