namespace DataForeman.Core.Entities;

public class Connection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // opcua-client | opcua-server | s7 | eip | mqtt | system
    public bool Enabled { get; set; } = true;
    public string ConfigData { get; set; } = "{}"; // JSON
    public bool IsSystemConnection { get; set; }
    public int MaxTagsPerGroup { get; set; } = 500;
    public int MaxConcurrentConnections { get; set; } = 8;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public virtual ICollection<TagMetadata> Tags { get; set; } = new List<TagMetadata>();
}

public class TagMetadata
{
    public int TagId { get; set; }
    public Guid ConnectionId { get; set; }
    public string DriverType { get; set; } = string.Empty; // EIP | OPCUA | S7 | MQTT | SYSTEM | INTERNAL
    public string TagPath { get; set; } = string.Empty;
    public string? TagName { get; set; }
    public bool IsSubscribed { get; set; }
    public bool IsDeleted { get; set; }
    public string? Status { get; set; } // active | pending_delete | deleting | deleted
    public bool? OriginalSubscribed { get; set; }
    public int? DeleteJobId { get; set; }
    public DateTime? DeleteStartedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int PollGroupId { get; set; } = 5;
    public string? DataType { get; set; }
    public int? UnitId { get; set; }
    public string? Description { get; set; }
    public string Metadata { get; set; } = "{}"; // JSON
    public bool OnChangeEnabled { get; set; }
    public float OnChangeDeadband { get; set; }
    public string OnChangeDeadbandType { get; set; } = "absolute"; // absolute | percent
    public int OnChangeHeartbeatMs { get; set; } = 60000;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Connection? Connection { get; set; }
    public virtual PollGroup? PollGroup { get; set; }
    public virtual UnitOfMeasure? Unit { get; set; }
}

public class PollGroup
{
    public int GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PollRateMs { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<TagMetadata> Tags { get; set; } = new List<TagMetadata>();
}

public class UnitOfMeasure
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<TagMetadata> Tags { get; set; } = new List<TagMetadata>();
}

/// <summary>
/// Time-series data point for tag values
/// </summary>
public class TagValue
{
    public long Id { get; set; }
    public int TagId { get; set; }
    public DateTime Timestamp { get; set; }
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public bool? BooleanValue { get; set; }
    public int Quality { get; set; } // 0 = Good, 1+ = Bad/Uncertain

    // Navigation
    public virtual TagMetadata? Tag { get; set; }
}
