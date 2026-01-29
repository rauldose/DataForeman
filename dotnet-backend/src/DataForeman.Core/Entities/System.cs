namespace DataForeman.Core.Entities;

/// <summary>
/// Background job for long-running operations
/// </summary>
public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty; // delete_tags, purge_points, etc.
    public string Status { get; set; } = "queued"; // queued | running | completed | failed | cancelling | cancelled
    public string? Params { get; set; } // JSON
    public string? Progress { get; set; } // JSON
    public string? Result { get; set; } // JSON
    public string? Error { get; set; }
    public bool CancellationRequested { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? WorkerId { get; set; }
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public DateTime? RunAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
}

/// <summary>
/// Audit event log
/// </summary>
public class AuditEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty; // success | failure | info
    public Guid? ActorUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Metadata { get; set; } // JSON

    // Navigation
    public virtual User? ActorUser { get; set; }
}

/// <summary>
/// System-wide settings (key-value store)
/// </summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = "{}"; // JSON
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Node library for Flow Studio
/// </summary>
public class NodeLibrary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LibraryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Manifest { get; set; } = "{}"; // JSON
    public bool Enabled { get; set; } = true;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public Guid? InstalledById { get; set; }
    public DateTime? LastLoadedAt { get; set; }
    public string? LoadErrors { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? InstalledBy { get; set; }
}

/// <summary>
/// Node category for Flow Studio palette
/// </summary>
public class NodeCategory
{
    public string CategoryKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = "📦";
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 99;
    public bool IsCore { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<NodeSection> Sections { get; set; } = new List<NodeSection>();
}

/// <summary>
/// Node section within a category
/// </summary>
public class NodeSection
{
    public string CategoryKey { get; set; } = string.Empty;
    public string SectionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 99;
    public bool IsCore { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual NodeCategory? Category { get; set; }
}
