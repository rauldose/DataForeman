namespace DataForeman.Shared.Models;

public class Flow
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? FolderId { get; set; }
    public bool Deployed { get; set; }
    public bool Shared { get; set; }
    public bool TestMode { get; set; }
    public bool TestDisableWrites { get; set; }
    public bool TestAutoExit { get; set; }
    public int TestAutoExitMinutes { get; set; } = 5;
    public string ExecutionMode { get; set; } = "continuous";
    public int ScanRateMs { get; set; } = 1000;
    public bool LiveValuesUseScanRate { get; set; }
    public bool LogsEnabled { get; set; }
    public int LogsRetentionDays { get; set; } = 30;
    public bool SaveUsageData { get; set; } = true;
    public string ExposedParameters { get; set; } = "[]";
    public Guid? ResourceChartId { get; set; }
    public string Definition { get; set; } = "{}";
    public string StaticData { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User? Owner { get; set; }
}
