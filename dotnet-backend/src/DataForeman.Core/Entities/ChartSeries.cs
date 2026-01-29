namespace DataForeman.Core.Entities;

/// <summary>
/// Represents a data series in a chart
/// </summary>
public class ChartSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChartId { get; set; }
    public int TagId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#4caf50";
    public string SeriesType { get; set; } = "line"; // line | bar | area | scatter | spline
    public int AxisIndex { get; set; } = 0; // 0 = primary Y-axis, 1+ = secondary axes
    public int DisplayOrder { get; set; } = 0;
    public bool Visible { get; set; } = true;
    public double? LineWidth { get; set; } = 2;
    public bool ShowMarkers { get; set; } = false;
    public double? MarkerSize { get; set; } = 6;
    public double? Opacity { get; set; } = 1.0;
    public string? AggregationMethod { get; set; } // avg | sum | min | max | last | first
    public int? AggregationInterval { get; set; } // seconds
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ChartConfig? Chart { get; set; }
    public virtual TagMetadata? Tag { get; set; }
    public virtual ChartAxis? Axis { get; set; }
}

/// <summary>
/// Represents an axis configuration in a chart
/// </summary>
public class ChartAxis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChartId { get; set; }
    public int AxisIndex { get; set; } = 0; // 0 = primary, 1+ = secondary
    public string AxisType { get; set; } = "Y"; // X | Y
    public string Position { get; set; } = "left"; // left | right | top | bottom
    public string? Label { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool AutoScale { get; set; } = true;
    public bool ShowGridLines { get; set; } = true;
    public string? GridLineStyle { get; set; } = "solid"; // solid | dashed | dotted
    public string? LabelFormat { get; set; } // e.g., "{value}°C", "{value:N2}"
    public bool Logarithmic { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ChartConfig? Chart { get; set; }
    public virtual ICollection<ChartSeries> Series { get; set; } = new List<ChartSeries>();
}
