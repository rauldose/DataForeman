using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Chart configuration for data visualization.
/// </summary>
public class ChartConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ChartType { get; set; } = "Line"; // Line, Bar, Area, Gauge
    public List<ChartSeries> Series { get; set; } = new();
    public ChartSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Series configuration within a chart.
/// </summary>
public class ChartSeries
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TagId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = "#3b82f6";
    public string LineType { get; set; } = "Solid"; // Solid, Dashed, Dotted
    public int LineWidth { get; set; } = 2;
    public double Opacity { get; set; } = 1.0;
    public bool Visible { get; set; } = true;
    public string? YAxisId { get; set; }
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
    public bool LiveEnabled { get; set; } = true;
    public int RefreshIntervalMs { get; set; } = 1000;
    public string TimeRange { get; set; } = "5m"; // 1m, 5m, 15m, 30m, 1h, 6h, 24h, 7d, custom
    public DateTime? CustomStartTime { get; set; }
    public DateTime? CustomEndTime { get; set; }
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
