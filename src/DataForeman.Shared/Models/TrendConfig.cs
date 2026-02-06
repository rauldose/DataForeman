using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Configuration for trend visualization.
/// </summary>
public class TrendConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TrendSeries> Series { get; set; } = new();
    public TrendTimeRange TimeRange { get; set; } = new();
    public TrendDisplayOptions DisplayOptions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A data series in a trend chart.
/// </summary>
public class TrendSeries
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string TagPath { get; set; } = string.Empty;
    public string Color { get; set; } = "#3b82f6";
    public TrendSeriesType Type { get; set; } = TrendSeriesType.Line;
    public bool Visible { get; set; } = true;
    public int YAxisIndex { get; set; } = 0;
}

/// <summary>
/// Time range configuration for trends.
/// </summary>
public class TrendTimeRange
{
    public TrendTimeRangeType Type { get; set; } = TrendTimeRangeType.Last1Hour;
    public DateTime? CustomStart { get; set; }
    public DateTime? CustomEnd { get; set; }
}

/// <summary>
/// Display options for trend charts.
/// </summary>
public class TrendDisplayOptions
{
    public bool ShowLegend { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool EnableZoom { get; set; } = true;
    public bool EnablePan { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 5;
    public string Theme { get; set; } = "light";
}

/// <summary>
/// Types of trend series visualization.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrendSeriesType
{
    Line,
    StepLine,
    Area,
    Column,
    Scatter
}

/// <summary>
/// Predefined time range options.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrendTimeRangeType
{
    Last15Minutes,
    Last1Hour,
    Last4Hours,
    Last12Hours,
    Last24Hours,
    Last7Days,
    Last30Days,
    Custom
}

/// <summary>
/// Root configuration file structure for trends.
/// </summary>
public class TrendsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<TrendConfig> Trends { get; set; } = new();
}
