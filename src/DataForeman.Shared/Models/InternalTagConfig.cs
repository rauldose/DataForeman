using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Context scope for internal tags (similar to Node-RED contexts).
/// </summary>
public enum ContextScope
{
    /// <summary>Global context - persists across all flows and executions.</summary>
    Global,
    
    /// <summary>Flow context - scoped to a specific flow.</summary>
    Flow,
    
    /// <summary>Node context - scoped to a specific node instance.</summary>
    Node
}

/// <summary>
/// Configuration for an internal tag.
/// Internal tags are virtual tags that exist within the system,
/// derived from or computed using field tags and other internal tags.
/// </summary>
public class InternalTagConfig
{
    /// <summary>Unique identifier for the tag.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Display name for the tag.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Path for accessing the tag (e.g., "global/temperature_avg" or "flow/my-flow/counter").</summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>Context scope where this tag lives.</summary>
    public ContextScope Scope { get; set; } = ContextScope.Global;
    
    /// <summary>Flow ID (required if Scope is Flow).</summary>
    public string? FlowId { get; set; }
    
    /// <summary>Node ID (required if Scope is Node).</summary>
    public string? NodeId { get; set; }
    
    /// <summary>Data type of the tag value (Boolean, Int16, Int32, Float, Double, String).</summary>
    public string DataType { get; set; } = "Double";
    
    /// <summary>Default value when the tag is first created.</summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>Optional description of the tag.</summary>
    public string? Description { get; set; }
    
    /// <summary>Whether to persist this tag's value across restarts.</summary>
    public bool Persistent { get; set; } = false;
    
    /// <summary>When the tag was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>When the tag was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Current value of an internal tag.
/// </summary>
public sealed record InternalTagValue
{
    /// <summary>The tag path.</summary>
    public required string Path { get; init; }
    
    /// <summary>The current value.</summary>
    public object? Value { get; init; }
    
    /// <summary>When the value was last updated.</summary>
    public DateTime TimestampUtc { get; init; }
    
    /// <summary>Quality indicator (0 = Good, 192 = Bad).</summary>
    public int Quality { get; init; } = 0;
    
    /// <summary>The context scope.</summary>
    public ContextScope Scope { get; init; }
}

/// <summary>
/// Root configuration file structure for internal tags.
/// </summary>
public class InternalTagsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    
    /// <summary>List of internal tag configurations.</summary>
    public List<InternalTagConfig> Tags { get; set; } = new();
}
