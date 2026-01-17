namespace DataForeman.Shared.Models;

public class TagMetadata
{
    public int TagId { get; set; }
    public Guid ConnectionId { get; set; }
    public required string DriverType { get; set; } // EIP, OPCUA, S7, MQTT, SYSTEM, INTERNAL
    public required string TagPath { get; set; }
    public string? TagName { get; set; }
    public bool IsSubscribed { get; set; }
    public bool IsDeleted { get; set; }
    public string? Status { get; set; }
    public bool? OriginalSubscribed { get; set; }
    public int? DeleteJobId { get; set; }
    public DateTime? DeleteStartedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int PollGroupId { get; set; } = 5;
    public string? DataType { get; set; }
    public int? UnitId { get; set; }
    public string? Description { get; set; }
    public string Metadata { get; set; } = "{}";
    public bool OnChangeEnabled { get; set; }
    public float OnChangeDeadband { get; set; }
    public string OnChangeDeadbandType { get; set; } = "absolute";
    public int OnChangeHeartbeatMs { get; set; } = 60000;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Connection? Connection { get; set; }
    public PollGroup? PollGroup { get; set; }
    public UnitOfMeasure? Unit { get; set; }
}
