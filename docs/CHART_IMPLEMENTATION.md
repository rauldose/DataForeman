# Chart Implementation Guide

## Overview

DataForeman now supports fully customizable, multi-axis, multi-channel charts with both real-time and historical data capabilities.

## Features Implemented

### ✅ Database Seeding
- **15 Industrial Tags** with realistic metadata
- **~150,000 Historical Data Points** (7 days × 15 tags × 1440 samples/day)
- **Realistic Patterns**: Daily cycles, production schedules, Gaussian noise, anomalies
- **Automatic Seeding**: Configurable via `SeedHistoricalData` app setting

### ✅ Multi-Axis Support
- **Multiple Y-Axes**: Primary (left) + unlimited secondary axes (right)
- **Independent Scaling**: Each axis with its own min/max/autoscale
- **Axis Labels**: Custom labels with unit formatting
- **Grid Lines**: Configurable per-axis with style options

### ✅ Multi-Channel (Series)
- **Unlimited Series per Chart**: Each assignable to any axis
- **Full Styling Control**: Color, line width, markers, opacity
- **Series Types**: Line, Bar, Area, Scatter, Spline
- **Display Ordering**: Stacking and z-index control

### ✅ Real-Time & Historical
- **Live Updates**: Configurable refresh intervals (1-60 seconds)
- **Time Modes**: Fixed, Rolling, Shifted windows
- **Historical Queries**: Time range filtering with pagination
- **Data Aggregation**: Avg, Sum, Min, Max, Last, First

## Database Schema

### New Entities

#### ChartSeries
```csharp
public class ChartSeries
{
    public Guid Id { get; set; }
    public Guid ChartId { get; set; }
    public int TagId { get; set; }
    public string Label { get; set; }
    public string Color { get; set; }
    public string SeriesType { get; set; }  // line, bar, area, scatter
    public int AxisIndex { get; set; }       // 0 = primary, 1+ = secondary
    public int DisplayOrder { get; set; }
    public bool Visible { get; set; }
    public double? LineWidth { get; set; }
    public bool ShowMarkers { get; set; }
    public double? Opacity { get; set; }
    // ... aggregation settings
}
```

#### ChartAxis
```csharp
public class ChartAxis
{
    public Guid Id { get; set; }
    public Guid ChartId { get; set; }
    public int AxisIndex { get; set; }      // 0 = primary, 1+ = secondary
    public string AxisType { get; set; }    // X, Y
    public string Position { get; set; }    // left, right, top, bottom
    public string? Label { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool AutoScale { get; set; }
    public bool ShowGridLines { get; set; }
    public string? LabelFormat { get; set; }
    public bool Logarithmic { get; set; }
}
```

#### Enhanced ChartConfig
```csharp
public class ChartConfig
{
    // ... existing properties
    public bool LiveEnabled { get; set; }
    public int RefreshInterval { get; set; }  // milliseconds
    public bool EnableLegend { get; set; }
    public string LegendPosition { get; set; }
    public bool EnableTooltip { get; set; }
    public bool EnableZoom { get; set; }
    public bool EnablePan { get; set; }
    
    // Navigation
    public virtual ICollection<ChartSeries> Series { get; set; }
    public virtual ICollection<ChartAxis> Axes { get; set; }
}
```

## API Reference

### Get Chart with Series and Axes
```http
GET /api/charts/{id}
```

**Response:**
```json
{
  "id": "30000000-0000-0000-0000-000000000003",
  "name": "Multi-Axis Process Monitor",
  "chartType": "line",
  "liveEnabled": true,
  "refreshInterval": 5000,
  "series": [
    {
      "id": "32000000-0000-0000-0000-000000000001",
      "tagId": 1,
      "tagPath": "Simulator/Tank1/Temperature",
      "label": "Tank 1 Temperature",
      "color": "#ff6384",
      "seriesType": "line",
      "axisIndex": 0,
      "lineWidth": 2,
      "showMarkers": true
    },
    {
      "id": "32000000-0000-0000-0000-000000000003",
      "tagId": 2,
      "tagPath": "Simulator/Tank1/Pressure",
      "label": "Tank 1 Pressure",
      "color": "#36a2eb",
      "seriesType": "line",
      "axisIndex": 1
    }
  ],
  "axes": [
    {
      "id": "31000000-0000-0000-0000-000000000001",
      "axisIndex": 0,
      "position": "left",
      "label": "Temperature (°C)",
      "autoScale": true,
      "showGridLines": true
    },
    {
      "id": "31000000-0000-0000-0000-000000000002",
      "axisIndex": 1,
      "position": "right",
      "label": "Pressure (kPa)",
      "autoScale": true,
      "showGridLines": false
    }
  ]
}
```

### Get Historical Data
```http
GET /api/charts/data?tagIds=1,2,3&from=2024-01-22T00:00:00Z&to=2024-01-29T00:00:00Z&limit=10000
```

**Response:**
```json
{
  "data": {
    "1": [
      {"timestamp": "2024-01-22T00:00:00Z", "value": 52.3, "quality": 0},
      {"timestamp": "2024-01-22T00:01:00Z", "value": 52.5, "quality": 0}
    ],
    "2": [
      {"timestamp": "2024-01-22T00:00:00Z", "value": 148.7, "quality": 0}
    ]
  },
  "from": "2024-01-22T00:00:00Z",
  "to": "2024-01-29T00:00:00Z",
  "count": 20160
}
```

### Get Available Tags
```http
GET /api/charts/tags
```

**Response:**
```json
{
  "tags": [
    {
      "tagId": 1,
      "tagPath": "Simulator/Tank1/Temperature",
      "tagName": "Tank1_Temperature",
      "description": "Tank 1 Temperature Sensor",
      "dataType": "Float",
      "unit": {
        "symbol": "°C",
        "name": "Degrees Celsius"
      }
    }
  ],
  "count": 15
}
```

### Add Series to Chart
```http
POST /api/charts/{chartId}/series
Content-Type: application/json

{
  "tagId": 1,
  "label": "Tank 1 Temperature",
  "color": "#ff6384",
  "seriesType": "line",
  "axisIndex": 0,
  "displayOrder": 0,
  "visible": true,
  "lineWidth": 2,
  "showMarkers": true,
  "markerSize": 6,
  "opacity": 1.0
}
```

### Add Axis to Chart
```http
POST /api/charts/{chartId}/axes
Content-Type: application/json

{
  "axisIndex": 1,
  "axisType": "Y",
  "position": "right",
  "label": "Pressure (kPa)",
  "autoScale": true,
  "showGridLines": false,
  "gridLineStyle": "dashed"
}
```

## Seeded Sample Data

### Tags (15 total)
1. **Tank 1**: Temperature, Pressure, Level, Flow, Pump Status
2. **Tank 2**: Temperature, Pressure, Level
3. **Motor 1**: Speed (RPM), Current (A), Power (kW)
4. **Process**: Production Rate, Quality Index, Efficiency, Alarm Count

### Historical Data Patterns
- **Temperature**: 20-80°C with daily sinusoidal cycle + noise
- **Pressure**: 100-200 kPa with hourly variations
- **Level**: 20-90% with slow drift
- **Flow Rate**: 10-100 L/min with daily pattern
- **Motor Speed**: 1200-1800 RPM with load variations
- **Production Rate**: 100-500 units/hour (weekday/weekend difference)
- **Quality**: 85-99% with small variations
- **Efficiency**: 70-95% with time-of-day patterns

### Demo Chart: "Multi-Axis Process Monitor"
Pre-configured with 4 series on 3 axes:
- **Left Axis (Temperature °C)**: Tank 1 & 2 temperature
- **Right Axis 1 (Pressure kPa)**: Tank 1 pressure
- **Right Axis 2 (Speed RPM)**: Motor 1 speed

## Configuration

### Enable Historical Data Seeding
Add to `appsettings.json` or environment variables:
```json
{
  "SeedHistoricalData": true
}
```

Or via environment variable:
```bash
export SeedHistoricalData=true
```

### Customize Seeding Parameters
Modify in `Program.cs`:
```csharp
await seeder.SeedHistoricalDataAsync(
    daysOfHistory: 7,      // Days of data to generate
    samplesPerHour: 60     // 60 = 1 per minute, 12 = 1 per 5 minutes
);
```

## Frontend Implementation Guide

### Rendering Multi-Axis Chart (Syncfusion)

```razor
<SfChart>
    @* Define X-axis *@
    <ChartPrimaryXAxis ValueType="ValueType.DateTime" />
    
    @* Define primary Y-axis (left) *@
    <ChartPrimaryYAxis Title="Temperature (°C)">
        <ChartAxisMajorGridLines Width="0.5" />
    </ChartPrimaryYAxis>
    
    @* Define secondary Y-axes (right) *@
    <ChartAxes>
        <ChartAxis Name="PressureAxis" 
                   Title="Pressure (kPa)" 
                   OpposedPosition="true">
        </ChartAxis>
        <ChartAxis Name="SpeedAxis" 
                   Title="Speed (RPM)" 
                   OpposedPosition="true">
        </ChartAxis>
    </ChartAxes>
    
    @* Add series *@
    <ChartSeriesCollection>
        <ChartSeries DataSource="@TempData" 
                     XName="Timestamp" 
                     YName="Value" 
                     Type="ChartSeriesType.Line"
                     Name="Temperature">
        </ChartSeries>
        
        <ChartSeries DataSource="@PressureData" 
                     XName="Timestamp" 
                     YName="Value" 
                     Type="ChartSeriesType.Line"
                     YAxisName="PressureAxis"
                     Name="Pressure">
        </ChartSeries>
        
        <ChartSeries DataSource="@SpeedData" 
                     XName="Timestamp" 
                     YName="Value" 
                     Type="ChartSeriesType.Line"
                     YAxisName="SpeedAxis"
                     Name="Motor Speed">
        </ChartSeries>
    </ChartSeriesCollection>
</SfChart>
```

### Fetching Data in Blazor

```csharp
private async Task LoadChartData(Guid chartId)
{
    // Get chart configuration
    var chartResponse = await Http.GetFromJsonAsync<ChartResponse>($"api/charts/{chartId}");
    
    // Get tag IDs from series
    var tagIds = chartResponse.Series.Select(s => s.TagId).ToArray();
    
    // Get historical data
    var from = DateTime.UtcNow.AddHours(-24);
    var to = DateTime.UtcNow;
    var dataResponse = await Http.GetFromJsonAsync<ChartDataResponse>(
        $"api/charts/data?tagIds={string.Join(",", tagIds)}&from={from:O}&to={to:O}"
    );
    
    // Map data to series
    foreach (var series in chartResponse.Series)
    {
        if (dataResponse.Data.TryGetValue(series.TagId.ToString(), out var points))
        {
            series.DataPoints = points.Select(p => new TimeSeriesPoint 
            {
                Timestamp = p.Timestamp,
                Value = p.Value
            }).ToList();
        }
    }
}
```

### Real-Time Updates (Timer-based)

```csharp
private Timer? _refreshTimer;

protected override void OnInitialized()
{
    if (Chart?.LiveEnabled == true)
    {
        _refreshTimer = new Timer(async _ => await RefreshData(), 
                                  null, 
                                  Chart.RefreshInterval, 
                                  Chart.RefreshInterval);
    }
}

private async Task RefreshData()
{
    // Fetch only new data points since last update
    var lastTimestamp = GetLastTimestamp();
    var dataResponse = await Http.GetFromJsonAsync<ChartDataResponse>(
        $"api/charts/data?tagIds={string.Join(",", tagIds)}&from={lastTimestamp:O}"
    );
    
    // Append new points and remove old ones (rolling window)
    AppendNewDataPoints(dataResponse.Data);
    await InvokeAsync(StateHasChanged);
}
```

## Performance Considerations

- **Data Pagination**: Use `limit` parameter to prevent large responses
- **Time Windows**: Request only visible time range
- **Aggregation**: Use aggregation for long time ranges
- **Caching**: Consider caching tag metadata
- **Batch Loading**: Load multiple tags in single request
- **Database Indexes**: Already configured on `(TagId, Timestamp)`

## Next Steps

1. **Enhanced UI**: Drag-drop series, axis configuration dialogs
2. **SignalR**: Real-time streaming instead of polling
3. **Templates**: Save/load chart configurations
4. **Export**: CSV, PNG, PDF export options
5. **Annotations**: Add markers, zones, notes
6. **Alerts**: Threshold-based visual indicators

## Testing

Run the historical data seeder:
```bash
cd dotnet-backend/src/DataForeman.API
dotnet run --SeedHistoricalData=true
```

Check seeded data:
```bash
# View charts
curl http://localhost:5000/api/charts

# Get multi-axis chart
curl http://localhost:5000/api/charts/30000000-0000-0000-0000-000000000003

# Get historical data
curl "http://localhost:5000/api/charts/data?tagIds=1,2,9&limit=100"
```
