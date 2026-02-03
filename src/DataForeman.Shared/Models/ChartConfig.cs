namespace DataForeman.Shared.Models;

/// <summary>
/// Configuration for a chart
/// </summary>
public class ChartConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public ChartType Type { get; set; } = ChartType.Line;
    public int RefreshRateMs { get; set; } = 1000;
    public TimeSpan HistoryWindow { get; set; } = TimeSpan.FromMinutes(5);
    public bool ShowRealtime { get; set; } = true;
    public List<ChartSeries> Series { get; set; } = new();
    public ChartAxisConfig XAxis { get; set; } = new();
    public ChartAxisConfig YAxis { get; set; } = new();
    public ChartAxisConfig? Y2Axis { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ChartType
{
    Line,
    Area,
    Bar,
    Scatter,
    StepLine
}

/// <summary>
/// A data series on a chart
/// </summary>
public class ChartSeries
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string TagId { get; set; } = "";  // Reference to TagConfig.Id
    public string ConnectionId { get; set; } = "";  // Reference to ConnectionConfig.Id
    public string Color { get; set; } = "#3b82f6";
    public int LineWidth { get; set; } = 2;
    public bool UseY2Axis { get; set; } = false;
    public bool Visible { get; set; } = true;
    public AggregationType Aggregation { get; set; } = AggregationType.None;
}

public enum AggregationType
{
    None,
    Average,
    Min,
    Max,
    Sum,
    Count
}

/// <summary>
/// Chart axis configuration
/// </summary>
public class ChartAxisConfig
{
    public string? Label { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool AutoScale { get; set; } = true;
    public string? Format { get; set; }  // e.g., "0.00", "HH:mm:ss"
    public bool ShowGrid { get; set; } = true;
}

/// <summary>
/// Container for all chart configurations
/// </summary>
public class ChartsFile
{
    public string Version { get; set; } = "1.0";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public List<ChartConfig> Charts { get; set; } = new();
}
