namespace DataForeman.Shared.Models;

/// <summary>
/// Configuration for a data processing flow
/// </summary>
public class FlowConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Enabled { get; set; } = false;
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowConnection> Connections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A node in the flow graph
/// </summary>
public class FlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Type { get; set; } = "";  // e.g., "tagInput", "math.add", "tagOutput"
    public string? Label { get; set; }
    public double X { get; set; }  // Position for visual editor
    public double Y { get; set; }
    public Dictionary<string, object?> Config { get; set; } = new();  // Node-specific config
}

/// <summary>
/// A connection between two nodes
/// </summary>
public class FlowConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FromNodeId { get; set; } = "";
    public string FromPort { get; set; } = "out";  // Output port name
    public string ToNodeId { get; set; } = "";
    public string ToPort { get; set; } = "in";  // Input port name
}

/// <summary>
/// Definition of a node type (for the palette)
/// </summary>
public class NodeDefinition
{
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public string Icon { get; set; } = "N";
    public string Color { get; set; } = "#6b7280";
    public List<PortDefinition> Inputs { get; set; } = new();
    public List<PortDefinition> Outputs { get; set; } = new();
    public List<PropertyDefinition> Properties { get; set; } = new();
}

public class PortDefinition
{
    public string Name { get; set; } = "";
    public string? Label { get; set; }
    public PortDataType DataType { get; set; } = PortDataType.Any;
}

public enum PortDataType
{
    Any,
    Number,
    Boolean,
    String,
    Object
}

public class PropertyDefinition
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public PropertyType Type { get; set; } = PropertyType.Text;
    public object? DefaultValue { get; set; }
    public bool Required { get; set; } = false;
    public List<string>? Options { get; set; }  // For dropdown type
}

public enum PropertyType
{
    Text,
    Number,
    Boolean,
    Dropdown,
    TagSelect,
    Code
}

/// <summary>
/// Container for all flow configurations
/// </summary>
public class FlowsFile
{
    public string Version { get; set; } = "1.0";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public List<FlowConfig> Flows { get; set; } = new();
}
