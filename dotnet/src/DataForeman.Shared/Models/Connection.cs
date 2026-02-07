using System.Text.Json;

namespace DataForeman.Shared.Models;

public class Connection
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // opcua-client, opcua-server, s7, eip, system
    public bool Enabled { get; set; } = true;
    public string ConfigData { get; set; } = "{}";
    public bool IsSystemConnection { get; set; }
    public int? MaxTagsPerGroup { get; set; } = 500;
    public int? MaxConcurrentConnections { get; set; } = 8;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    
    public ICollection<TagMetadata>? Tags { get; set; }
}
