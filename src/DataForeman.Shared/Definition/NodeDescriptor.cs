// DataForeman Platform - AI Agent Implementation Directives
// Section 2.4: NODE DESCRIPTOR (CONTRACT)
// Declares node type, display name, category, input/output ports, config schema.
// Ports are explicit and have stable names. No implicit ports. Ever.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataForeman.Shared.Definition;

/// <summary>
/// Node descriptor declares the contract for a node type.
/// This is the metadata artifact that describes what a node IS.
/// </summary>
public sealed class NodeDescriptor
{
    /// <summary>Node type string (unique identifier).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Display name for the node.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>Category for palette organization.</summary>
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>Node description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>FontAwesome icon name.</summary>
    [JsonPropertyName("icon")]
    public string Icon { get; init; } = "fa-cube";

    /// <summary>Node color (hex).</summary>
    [JsonPropertyName("color")]
    public string Color { get; init; } = "#3498db";

    /// <summary>Declared input ports (explicit, stable names).</summary>
    [JsonPropertyName("inputPorts")]
    public IReadOnlyList<PortDescriptor> InputPorts { get; init; } = Array.Empty<PortDescriptor>();

    /// <summary>Declared output ports (explicit, stable names).</summary>
    [JsonPropertyName("outputPorts")]
    public IReadOnlyList<PortDescriptor> OutputPorts { get; init; } = Array.Empty<PortDescriptor>();

    /// <summary>Configuration schema for validation.</summary>
    [JsonPropertyName("configSchema")]
    public NodeConfigSchema? ConfigSchema { get; init; }

    /// <summary>Whether this node type can be used in normal flows (false for subflow.input/subflow.output).</summary>
    [JsonPropertyName("isFlowNode")]
    public bool IsFlowNode { get; init; } = true;

    /// <summary>Whether this node is a trigger (starts flow execution).</summary>
    [JsonPropertyName("isTrigger")]
    public bool IsTrigger { get; init; }
}

/// <summary>
/// Port descriptor for node input/output ports.
/// </summary>
public sealed class PortDescriptor
{
    /// <summary>Port name (stable identifier, never changes).</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Display label.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>Port direction.</summary>
    [JsonPropertyName("direction")]
    public PortDirection Direction { get; init; }

    /// <summary>Connection cardinality.</summary>
    [JsonPropertyName("cardinality")]
    public PortCardinality Cardinality { get; init; } = PortCardinality.Multiple;

    /// <summary>Whether this port is required to be connected.</summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>Port description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Configuration schema for node validation.
/// </summary>
public sealed class NodeConfigSchema
{
    /// <summary>Configuration properties.</summary>
    [JsonPropertyName("properties")]
    public IReadOnlyList<ConfigProperty> Properties { get; init; } = Array.Empty<ConfigProperty>();
}

/// <summary>
/// Configuration property definition.
/// </summary>
public sealed class ConfigProperty
{
    /// <summary>Property name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Display label.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>Property type (string, number, boolean, tagPath, select).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>Whether property is required.</summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>Default value.</summary>
    [JsonPropertyName("defaultValue")]
    public JsonElement? DefaultValue { get; init; }

    /// <summary>Options for select type.</summary>
    [JsonPropertyName("options")]
    public IReadOnlyList<SelectOption>? Options { get; init; }

    /// <summary>Property description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Select option for dropdown properties.
/// </summary>
public sealed class SelectOption
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }
}
