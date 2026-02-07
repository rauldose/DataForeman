using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Chart configuration for data visualization with multi-axis support.
/// </summary>
public class ChartConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ChartType { get; set; } = "Line"; // Line, Bar, Area, Gauge
    public string? OwnerId { get; set; }
    public bool IsShared { get; set; }
    public List<ChartSeriesConfig> Series { get; set; } = new();
    public List<ChartAxisConfig> YAxes { get; set; } = new();
    public ChartSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Y-Axis configuration for multi-axis charts.
/// </summary>
public class ChartAxisConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = "Left"; // Left, Right
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string? Unit { get; set; }
    public string Color { get; set; } = "#ffffff";
    public bool AutoScale { get; set; } = true;
    public string LabelFormat { get; set; } = "0.##";
}

/// <summary>
/// Series configuration within a chart.
/// </summary>
public class ChartSeriesConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TagId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = "#3b82f6";
    public string LineType { get; set; } = "Solid"; // Solid, Dashed, Dotted
    public double LineWidth { get; set; } = 2.0;
    public double Opacity { get; set; } = 1.0;
    public bool Visible { get; set; } = true;
    public string? YAxisId { get; set; } // Reference to ChartAxisConfig.Id
}

/// <summary>
/// Chart display settings.
/// </summary>
public class ChartSettings
{
    public bool ShowLegend { get; set; } = true;
    public string LegendPosition { get; set; } = "Bottom"; // Top, Bottom, Left, Right
    public bool EnableZoom { get; set; } = true;
    public bool EnablePan { get; set; } = true;
    
    // Trending mode
    public TrendingMode Mode { get; set; } = TrendingMode.Realtime;
    
    // Realtime settings
    public int RefreshIntervalMs { get; set; } = 1000;
    public int RealtimeWindowSeconds { get; set; } = 300; // 5 minutes default
    
    // Historical settings
    public DateTime? HistoricalStartTime { get; set; }
    public DateTime? HistoricalEndTime { get; set; }
    public string TimeRange { get; set; } = "5m"; // 1m, 5m, 15m, 30m, 1h, 6h, 24h, 7d, custom
}

/// <summary>
/// Chart trending mode - realtime or historical.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrendingMode
{
    Realtime,
    Historical
}

/// <summary>
/// Root configuration file structure for charts.
/// </summary>
public class ChartsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<ChartConfig> Charts { get; set; } = new();
}
