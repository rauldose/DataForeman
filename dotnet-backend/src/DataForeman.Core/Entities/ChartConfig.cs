namespace DataForeman.Core.Entities;

public class ChartConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? FolderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChartType { get; set; } = "line";
    public bool IsSystemChart { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsShared { get; set; }
    public string TimeMode { get; set; } = "fixed"; // fixed | rolling | shifted
    public long? TimeDuration { get; set; } // Duration in milliseconds
    public long TimeOffset { get; set; } // Offset in milliseconds
    public bool LiveEnabled { get; set; }
    public bool ShowTimeBadge { get; set; } = true;
    public DateTime? TimeFrom { get; set; }
    public DateTime? TimeTo { get; set; }
    public long? TimeRangeMs { get; set; } // Legacy field
    public string Options { get; set; } = "{}"; // JSON for chart options
    public int RefreshInterval { get; set; } = 5000; // milliseconds for live data refresh
    public bool EnableLegend { get; set; } = true;
    public string LegendPosition { get; set; } = "bottom"; // top | bottom | left | right
    public bool EnableTooltip { get; set; } = true;
    public bool EnableZoom { get; set; } = true;
    public bool EnablePan { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
    public virtual ChartFolder? Folder { get; set; }
    public virtual ICollection<ChartSeries> Series { get; set; } = new List<ChartSeries>();
    public virtual ICollection<ChartAxis> Axes { get; set; } = new List<ChartAxis>();
}

public class ChartFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentFolderId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User? User { get; set; }
    public virtual ChartFolder? ParentFolder { get; set; }
    public virtual ICollection<ChartFolder> ChildFolders { get; set; } = new List<ChartFolder>();
    public virtual ICollection<ChartConfig> Charts { get; set; } = new List<ChartConfig>();
}
