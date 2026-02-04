// DataForeman Platform - AI Agent Implementation Directives
// Section 2.3: NODE DEFINITION (JSON)
// Stored inside a flow file. Position is editor-only. No runtime data.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataForeman.Shared.Definition;

/// <summary>
/// JSON Schema version for flow definitions.
/// All schemas are versioned. Unknown fields must be preserved.
/// </summary>
public static class SchemaVersion
{
    public const string CurrentFlow = "1.0.0";
    public const string CurrentTemplate = "1.0.0";
    public const string CurrentSubflow = "1.0.0";
}

/// <summary>
/// Node definition as persisted in JSON.
/// This is the Definition Layer artifact - never contains runtime state.
/// </summary>
public sealed class NodeDefinition
{
    /// <summary>Unique identifier for this node instance.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Node type string (immutable after creation).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>User-defined display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Node-specific configuration (JSON object).</summary>
    [JsonPropertyName("config")]
    public JsonElement? Config { get; set; }

    /// <summary>Editor-only position data. NEVER used at runtime.</summary>
    [JsonPropertyName("position")]
    public NodePosition Position { get; set; } = new();

    /// <summary>Whether this node is disabled (skipped at runtime).</summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>Preserve unknown fields for forward compatibility.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>Gets typed configuration.</summary>
    public T? GetConfig<T>() where T : class
    {
        if (Config == null || Config.Value.ValueKind == JsonValueKind.Null)
            return null;
        return Config.Value.Deserialize<T>();
    }

    /// <summary>Sets typed configuration.</summary>
    public void SetConfig<T>(T config) where T : class
    {
        var json = JsonSerializer.Serialize(config);
        Config = JsonDocument.Parse(json).RootElement.Clone();
    }
}

/// <summary>
/// Editor-only position data. Part of Definition Layer but excluded from Runtime.
/// </summary>
public sealed class NodePosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

/// <summary>
/// Wire definition connecting two ports.
/// </summary>
public sealed class WireDefinition
{
    /// <summary>Unique wire identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Source node ID.</summary>
    [JsonPropertyName("sourceNodeId")]
    public required string SourceNodeId { get; set; }

    /// <summary>Source port name (must be declared in NodeDescriptor).</summary>
    [JsonPropertyName("sourcePort")]
    public required string SourcePort { get; set; }

    /// <summary>Target node ID.</summary>
    [JsonPropertyName("targetNodeId")]
    public required string TargetNodeId { get; set; }

    /// <summary>Target port name (must be declared in NodeDescriptor).</summary>
    [JsonPropertyName("targetPort")]
    public required string TargetPort { get; set; }

    /// <summary>Preserve unknown fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Complete flow definition (persisted JSON).
/// </summary>
public sealed class FlowDefinition
{
    /// <summary>Schema version for migrations.</summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = SchemaVersion.CurrentFlow;

    /// <summary>Unique flow identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Flow name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Flow description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether flow is enabled for execution.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>All nodes in this flow.</summary>
    [JsonPropertyName("nodes")]
    public List<NodeDefinition> Nodes { get; set; } = new();

    /// <summary>All wires connecting nodes.</summary>
    [JsonPropertyName("wires")]
    public List<WireDefinition> Wires { get; set; } = new();

    /// <summary>Flow-level metadata.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>Preserve unknown fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Flow template definition (design-time only).
/// Templates are parameterized blueprints used to generate new flows.
/// They exist only at design time and are instantiated into standalone flows.
/// </summary>
public sealed class FlowTemplateDefinition
{
    /// <summary>Schema version.</summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = SchemaVersion.CurrentTemplate;

    /// <summary>Template identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Template name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Template description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Template category for organization.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    /// <summary>Declared parameters for instantiation.</summary>
    [JsonPropertyName("parameters")]
    public List<TemplateParameter> Parameters { get; set; } = new();

    /// <summary>Template nodes (with placeholder references).</summary>
    [JsonPropertyName("nodes")]
    public List<NodeDefinition> Nodes { get; set; } = new();

    /// <summary>Template wires.</summary>
    [JsonPropertyName("wires")]
    public List<WireDefinition> Wires { get; set; } = new();

    /// <summary>Preserve unknown fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Template parameter definition.
/// </summary>
public sealed class TemplateParameter
{
    /// <summary>Parameter name (used as placeholder key).</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>Parameter display label.</summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Parameter type (string, number, boolean, tagPath).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>Default value.</summary>
    [JsonPropertyName("defaultValue")]
    public JsonElement? DefaultValue { get; set; }

    /// <summary>Whether parameter is required.</summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}

/// <summary>
/// Subflow definition (runtime reuse).
/// Subflows are reusable executable graphs that behave like nodes.
/// </summary>
public sealed class SubflowDefinition
{
    /// <summary>Schema version.</summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = SchemaVersion.CurrentSubflow;

    /// <summary>Subflow identifier (used as node type).</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Subflow name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Subflow description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Subflow category.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Subflows";

    /// <summary>Subflow icon (FA icon name).</summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "fa-cube";

    /// <summary>Subflow color.</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#9b59b6";

    /// <summary>Declared input ports (mapped to subflow.input nodes).</summary>
    [JsonPropertyName("inputPorts")]
    public List<PortDefinition> InputPorts { get; set; } = new();

    /// <summary>Declared output ports (mapped to subflow.output nodes).</summary>
    [JsonPropertyName("outputPorts")]
    public List<PortDefinition> OutputPorts { get; set; } = new();

    /// <summary>Internal nodes (including subflow.input/subflow.output).</summary>
    [JsonPropertyName("nodes")]
    public List<NodeDefinition> Nodes { get; set; } = new();

    /// <summary>Internal wires.</summary>
    [JsonPropertyName("wires")]
    public List<WireDefinition> Wires { get; set; } = new();

    /// <summary>Preserve unknown fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Port definition for nodes and subflows.
/// Ports are explicit and have stable names.
/// </summary>
public sealed class PortDefinition
{
    /// <summary>Port name (stable identifier).</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>Port display label.</summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Port direction.</summary>
    [JsonPropertyName("direction")]
    public PortDirection Direction { get; set; }

    /// <summary>Port cardinality (single or multiple connections).</summary>
    [JsonPropertyName("cardinality")]
    public PortCardinality Cardinality { get; set; } = PortCardinality.Multiple;
}

/// <summary>
/// Port direction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortDirection
{
    Input,
    Output
}

/// <summary>
/// Port cardinality.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortCardinality
{
    /// <summary>Only one connection allowed.</summary>
    Single,
    /// <summary>Multiple connections allowed.</summary>
    Multiple
}
