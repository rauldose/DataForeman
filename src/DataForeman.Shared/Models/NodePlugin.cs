using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Defines the metadata for a flow node plugin.
/// This enables extensible node types that can be loaded dynamically.
/// </summary>
public class NodePluginDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "fa-solid fa-question";
    public string Color { get; set; } = "#9ca3af";
    public int InputCount { get; set; }
    public int OutputCount { get; set; }
    public List<NodePropertyDefinition> Properties { get; set; } = new();
    public string Version { get; set; } = "1.0.0";
    public bool IsBuiltIn { get; set; } = true;
}

/// <summary>
/// Defines a configurable property for a node plugin
/// </summary>
public class NodePropertyDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PropertyType Type { get; set; } = PropertyType.Text;
    public string DefaultValue { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool Required { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public List<SelectOption> Options { get; set; } = new();
    public string Group { get; set; } = "General";
    public int Order { get; set; }
    public bool Advanced { get; set; }
}

public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    Text,
    TextArea,
    Integer,
    Decimal,
    Boolean,
    Select,
    TagPath,
    Color,
    Cron,
    Json,
    ReadOnly,
    Code
}
