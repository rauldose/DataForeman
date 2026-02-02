using System.Text.Json.Serialization;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Defines the metadata for a flow node plugin.
/// This enables extensible node types that can be loaded dynamically.
/// </summary>
public class NodePluginDefinition
{
    /// <summary>
    /// Unique identifier for the node type (e.g., "trigger-manual", "math-add")
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name shown in the palette and properties panel
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Short label for compact display in palette
    /// </summary>
    public string ShortLabel { get; set; } = string.Empty;
    
    /// <summary>
    /// Category for grouping in the palette (e.g., "Triggers", "Math", "Output")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what the node does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon badge text (e.g., "[M]", "[+]", "[DB]")
    /// </summary>
    public string Icon { get; set; } = "[?]";
    
    /// <summary>
    /// Color for the node border/accent (hex color)
    /// </summary>
    public string Color { get; set; } = "#9ca3af";
    
    /// <summary>
    /// Number of input ports
    /// </summary>
    public int InputCount { get; set; }
    
    /// <summary>
    /// Number of output ports
    /// </summary>
    public int OutputCount { get; set; }
    
    /// <summary>
    /// List of configurable properties for this node type
    /// </summary>
    public List<NodePropertyDefinition> Properties { get; set; } = new();
    
    /// <summary>
    /// Version of this plugin definition
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Author or source of this plugin
    /// </summary>
    public string Author { get; set; } = "DataForeman";
    
    /// <summary>
    /// Whether this is a built-in node or external plugin
    /// </summary>
    public bool IsBuiltIn { get; set; } = true;
}

/// <summary>
/// Defines a configurable property for a node plugin
/// </summary>
public class NodePropertyDefinition
{
    /// <summary>
    /// Property key used in node data storage
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Display label for the property
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of input control to render
    /// </summary>
    public PropertyType Type { get; set; } = PropertyType.Text;
    
    /// <summary>
    /// Default value for the property
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;
    
    /// <summary>
    /// Placeholder text for text inputs
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;
    
    /// <summary>
    /// Help text shown below the input
    /// </summary>
    public string HelpText { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this property is required
    /// </summary>
    public bool Required { get; set; }
    
    /// <summary>
    /// For numeric types: minimum value
    /// </summary>
    public double? Min { get; set; }
    
    /// <summary>
    /// For numeric types: maximum value
    /// </summary>
    public double? Max { get; set; }
    
    /// <summary>
    /// For numeric types: step increment
    /// </summary>
    public double? Step { get; set; }
    
    /// <summary>
    /// For dropdown/select types: list of options
    /// </summary>
    public List<SelectOption> Options { get; set; } = new();
    
    /// <summary>
    /// Group name for organizing properties in sections
    /// </summary>
    public string Group { get; set; } = "General";
    
    /// <summary>
    /// Display order within the group
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Whether to show this property in advanced mode only
    /// </summary>
    public bool Advanced { get; set; }
}

/// <summary>
/// Option for dropdown/select properties
/// </summary>
public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Types of property input controls
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    /// <summary>Single-line text input</summary>
    Text,
    /// <summary>Multi-line text area</summary>
    TextArea,
    /// <summary>Integer number input</summary>
    Integer,
    /// <summary>Decimal number input</summary>
    Decimal,
    /// <summary>Boolean checkbox</summary>
    Boolean,
    /// <summary>Dropdown selection</summary>
    Select,
    /// <summary>Tag path picker</summary>
    TagPath,
    /// <summary>Color picker</summary>
    Color,
    /// <summary>Cron expression input</summary>
    Cron,
    /// <summary>JSON editor</summary>
    Json,
    /// <summary>Read-only display value</summary>
    ReadOnly,
    /// <summary>Code editor for scripts</summary>
    Code
}
