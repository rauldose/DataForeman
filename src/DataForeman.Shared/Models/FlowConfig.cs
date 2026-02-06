using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Returns a list of validation warnings (empty = valid).
    /// </summary>
    public List<string> Validate()
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            warnings.Add($"Flow '{Id}': Name is required");
        if (ScanRateMs < 10)
            warnings.Add($"Flow '{Name}': ScanRateMs ({ScanRateMs}) should be >= 10");
        if (Enabled && Nodes.Count == 0)
            warnings.Add($"Flow '{Name}': Enabled but has no nodes");
        // Check for edges referencing non-existent nodes
        var nodeIds = new HashSet<string>(Nodes.Select(n => n.Id));
        foreach (var edge in Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
                warnings.Add($"Flow '{Name}': Edge '{edge.Id}' references missing source node '{edge.SourceNodeId}'");
            if (!nodeIds.Contains(edge.TargetNodeId))
                warnings.Add($"Flow '{Name}': Edge '{edge.Id}' references missing target node '{edge.TargetNodeId}'");
        }
        return warnings;
    }

    /// <summary>
    /// Computes a deterministic content hash of the flow's structural definition
    /// (nodes, edges, properties, enabled state, scan rate).  Excludes timestamps
    /// so that re-saving without changes produces the same hash.
    /// </summary>
    public string ComputeContentHash()
    {
        // Normalize property values to their JSON text representation so that
        // string "0" and JsonElement(String,"0") produce the same hash.
        static string NormalizeValue(object? v) => v switch
        {
            null => "null",
            JsonElement je => je.GetRawText(),
            string s => JsonSerializer.Serialize(s),
            bool b => b ? "true" : "false",
            _ => JsonSerializer.Serialize(v)
        };

        var canonical = new
        {
            Name,
            Enabled,
            ScanRateMs,
            Nodes = Nodes
                .OrderBy(n => n.Id)
                .Select(n => new
                {
                    n.Id, n.Type, n.Label,
                    Props = n.Properties
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new { kv.Key, Value = NormalizeValue(kv.Value) })
                        .ToList()
                })
                .ToList(),
            Edges = Edges
                .OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.SourceNodeId, e.SourcePortId, e.TargetNodeId, e.TargetPortId })
                .ToList()
        };

        var json = JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = false });
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        // First 8 bytes (16 hex chars) provide 2^64 uniqueness — sufficient for
        // drift detection across typical deployment sizes.  A full 32-byte hash
        // is unnecessary here since false positives merely delay a UI refresh.
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }
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
    public string SourcePortId { get; set; } = "output-0";
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = "input-0";
}

/// <summary>
/// Subflow - a reusable group of nodes that acts as a single node.
/// </summary>
public class SubflowConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#8b5cf6"; // Purple for subflows
    public string Icon { get; set; } = "fa-solid fa-object-group";
    public int InputCount { get; set; } = 1;
    public int OutputCount { get; set; } = 1;
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Flow template - a reusable flow pattern that can be applied.
/// </summary>
public class FlowTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Custom";
    public string Icon { get; set; } = "fa-solid fa-file-code";
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

/// <summary>
/// Root configuration file structure for subflows.
/// </summary>
public class SubflowsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<SubflowConfig> Subflows { get; set; } = new();
}

/// <summary>
/// Root configuration file structure for flow templates.
/// </summary>
public class FlowTemplatesFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<FlowTemplate> Templates { get; set; } = new();
}
