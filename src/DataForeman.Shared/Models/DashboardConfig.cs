using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Dashboard configuration with flexible panel grid layout (Grafana-like).
/// </summary>
public class DashboardConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<DashboardPanel> Panels { get; set; } = new();
    public List<DashboardVariable> Variables { get; set; } = new();
    public DashboardSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A panel within a dashboard - can be chart, gauge, table, etc.
/// </summary>
public class DashboardPanel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public PanelType Type { get; set; } = PanelType.Chart;
    
    // Grid position (12-column grid, row height 50px)
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridWidth { get; set; } = 6;
    public int GridHeight { get; set; } = 4;
    
    // Panel-specific configuration
    public ChartPanelConfig? ChartConfig { get; set; }
    public GaugePanelConfig? GaugeConfig { get; set; }
    public StatPanelConfig? StatConfig { get; set; }
    public TablePanelConfig? TableConfig { get; set; }
}

/// <summary>
/// Dashboard panel types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PanelType
{
    Chart,
    Gauge,
    Stat,
    Table,
    Text
}

/// <summary>
/// Chart panel configuration
/// </summary>
public class ChartPanelConfig
{
    public string ChartType { get; set; } = "Line"; // Line, Area, Bar, Scatter
    public List<PanelDataSource> DataSources { get; set; } = new();
    public List<ChartAxisConfig> YAxes { get; set; } = new();
    public bool ShowLegend { get; set; } = true;
    public string LegendPosition { get; set; } = "Bottom";
    public bool EnableZoom { get; set; } = true;
    public int LineWidth { get; set; } = 2;
    public bool FillArea { get; set; } = false;
}

/// <summary>
/// Gauge panel configuration
/// </summary>
public class GaugePanelConfig
{
    public string TagId { get; set; } = string.Empty;
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;
    public string Unit { get; set; } = string.Empty;
    public List<GaugeThreshold> Thresholds { get; set; } = new()
    {
        new() { Value = 0, Color = "#22c55e" },
        new() { Value = 60, Color = "#f59e0b" },
        new() { Value = 80, Color = "#ef4444" }
    };
    public bool ShowValue { get; set; } = true;
    public int Decimals { get; set; } = 1;
    public GaugeType GaugeType { get; set; } = GaugeType.Radial;
}

/// <summary>
/// Gauge threshold for color ranges
/// </summary>
public class GaugeThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "#22c55e";
}

/// <summary>
/// Gauge display type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GaugeType
{
    Radial,
    Linear,
    Arc
}

/// <summary>
/// Single stat panel configuration
/// </summary>
public class StatPanelConfig
{
    public string TagId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Decimals { get; set; } = 2;
    public List<GaugeThreshold> Thresholds { get; set; } = new();
    public StatValueType ValueType { get; set; } = StatValueType.Current;
    public bool ShowSparkline { get; set; } = false;
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Stat value calculation type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatValueType
{
    Current,
    Average,
    Min,
    Max,
    Sum,
    Delta
}

/// <summary>
/// Table panel configuration
/// </summary>
public class TablePanelConfig
{
    public List<string> TagIds { get; set; } = new();
    public bool ShowTimestamp { get; set; } = true;
    public bool ShowQuality { get; set; } = true;
    public int MaxRows { get; set; } = 10;
    public bool AutoRefresh { get; set; } = true;
}

/// <summary>
/// Data source for a panel
/// </summary>
public class PanelDataSource
{
    public string TagId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = "#3b82f6";
    public string? YAxisId { get; set; }
    public double LineWidth { get; set; } = 2.0;
}

/// <summary>
/// Dashboard variable for templating
/// </summary>
public class DashboardVariable
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public VariableType Type { get; set; } = VariableType.Custom;
    public string Query { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public string CurrentValue { get; set; } = string.Empty;
    public bool Multi { get; set; } = false;
    public bool IncludeAll { get; set; } = false;
}

/// <summary>
/// Dashboard variable types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VariableType
{
    Custom,
    Query,
    Interval,
    TextBox,
    Constant
}

/// <summary>
/// Dashboard display settings
/// </summary>
public class DashboardSettings
{
    public TrendingMode DefaultMode { get; set; } = TrendingMode.Realtime;
    public int RefreshIntervalMs { get; set; } = 1000;
    public string TimeRange { get; set; } = "5m";
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public bool AutoRefresh { get; set; } = true;
    public string Theme { get; set; } = "dark";
    public bool Editable { get; set; } = true;
}

/// <summary>
/// Root configuration file structure for dashboards.
/// </summary>
public class DashboardsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<DashboardConfig> Dashboards { get; set; } = new();
}
