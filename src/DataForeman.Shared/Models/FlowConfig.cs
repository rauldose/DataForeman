using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Flow configuration for data processing workflows.
/// </summary>
public class FlowConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = false;
    public int ScanRateMs { get; set; } = 1000;
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Node within a flow.
/// </summary>
public class FlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // TagInput, TagOutput, Math, Gate, etc.
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Edge connecting two nodes in a flow.
/// </summary>
public class FlowEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = "output";
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = "input";
}

/// <summary>
/// Root configuration file structure for flows.
/// </summary>
public class FlowsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<FlowConfig> Flows { get; set; } = new();
}
