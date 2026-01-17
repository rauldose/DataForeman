namespace DataForeman.Shared.Models;

public class ChartConfig
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? FolderId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string ChartType { get; set; } = "line";
    public bool IsSystemChart { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsShared { get; set; }
    public string TimeMode { get; set; } = "fixed"; // fixed, rolling, shifted
    public long? TimeDuration { get; set; }
    public long TimeOffset { get; set; }
    public bool LiveEnabled { get; set; }
    public bool ShowTimeBadge { get; set; } = true;
    public DateTime? TimeFrom { get; set; }
    public DateTime? TimeTo { get; set; }
    public long? TimeRangeMs { get; set; }
    public string Options { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User? User { get; set; }
}
