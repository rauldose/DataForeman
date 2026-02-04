using System.Text.Json;
using System.Text.Json.Serialization;
using DataForeman.Shared.Models;

namespace DataForeman.App.Services;

/// <summary>
/// Service for managing JSON configuration files with auto-save capability.
/// Shared configuration with the Engine via JSON files.
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;
    private readonly ILogger<ConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private ConnectionsFile _connections = new();
    private ChartsFile _charts = new();
    private FlowsFile _flows = new();
    private DashboardsFile _dashboards = new();
    
    public event Action? OnConfigurationChanged;

    public ConfigService(IConfiguration configuration, ILogger<ConfigService> logger)
    {
        _configDirectory = configuration.GetValue<string>("ConfigDirectory") 
            ?? Path.Combine(AppContext.BaseDirectory, "config");
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureConfigDirectoryExists();
    }

    public string ConfigDirectory => _configDirectory;
    public IReadOnlyList<ConnectionConfig> Connections => _connections.Connections.AsReadOnly();
    public IReadOnlyList<ChartConfig> Charts => _charts.Charts.AsReadOnly();
    public IReadOnlyList<FlowConfig> Flows => _flows.Flows.AsReadOnly();
    public IReadOnlyList<DashboardConfig> Dashboards => _dashboards.Dashboards.AsReadOnly();

    /// <summary>
    /// Loads all configuration files.
    /// </summary>
    public async Task LoadAllAsync()
    {
        await LoadConnectionsAsync();
        await LoadChartsAsync();
        await LoadFlowsAsync();
        await LoadDashboardsAsync();
        _logger.LogInformation("All configuration files loaded from {Directory}", _configDirectory);
    }

    /// <summary>
    /// Loads connections configuration.
    /// </summary>
    public async Task LoadConnectionsAsync()
    {
        var filePath = GetConfigFilePath("connections.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                _connections = JsonSerializer.Deserialize<ConnectionsFile>(json, _jsonOptions) ?? new ConnectionsFile();
                _logger.LogInformation("Loaded {Count} connections from {FilePath}", _connections.Connections.Count, filePath);
            }
            else
            {
                _connections = CreateDefaultConnections();
                await SaveConnectionsAsync();
                _logger.LogInformation("Created default connections configuration");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading connections from {FilePath}", filePath);
            _connections = new ConnectionsFile();
        }
    }

    /// <summary>
    /// Loads charts configuration.
    /// </summary>
    public async Task LoadChartsAsync()
    {
        var filePath = GetConfigFilePath("charts.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                _charts = JsonSerializer.Deserialize<ChartsFile>(json, _jsonOptions) ?? new ChartsFile();
                _logger.LogInformation("Loaded {Count} charts from {FilePath}", _charts.Charts.Count, filePath);
            }
            else
            {
                _charts = new ChartsFile();
                await SaveChartsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading charts from {FilePath}", filePath);
            _charts = new ChartsFile();
        }
    }

    /// <summary>
    /// Loads flows configuration.
    /// </summary>
    public async Task LoadFlowsAsync()
    {
        var filePath = GetConfigFilePath("flows.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                _flows = JsonSerializer.Deserialize<FlowsFile>(json, _jsonOptions) ?? new FlowsFile();
                _logger.LogInformation("Loaded {Count} flows from {FilePath}", _flows.Flows.Count, filePath);
            }
            else
            {
                _flows = new FlowsFile();
                await SaveFlowsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading flows from {FilePath}", filePath);
            _flows = new FlowsFile();
        }
    }

    /// <summary>
    /// Saves connections configuration.
    /// </summary>
    public async Task SaveConnectionsAsync()
    {
        var filePath = GetConfigFilePath("connections.json");
        try
        {
            var json = JsonSerializer.Serialize(_connections, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved connections to {FilePath}", filePath);
            OnConfigurationChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving connections to {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Saves charts configuration.
    /// </summary>
    public async Task SaveChartsAsync()
    {
        var filePath = GetConfigFilePath("charts.json");
        try
        {
            var json = JsonSerializer.Serialize(_charts, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved charts to {FilePath}", filePath);
            OnConfigurationChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving charts to {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Saves flows configuration.
    /// </summary>
    public async Task SaveFlowsAsync()
    {
        var filePath = GetConfigFilePath("flows.json");
        try
        {
            var json = JsonSerializer.Serialize(_flows, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved flows to {FilePath}", filePath);
            OnConfigurationChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving flows to {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Loads dashboards configuration.
    /// </summary>
    public async Task LoadDashboardsAsync()
    {
        var filePath = GetConfigFilePath("dashboards.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                _dashboards = JsonSerializer.Deserialize<DashboardsFile>(json, _jsonOptions) ?? new DashboardsFile();
                _logger.LogInformation("Loaded {Count} dashboards from {FilePath}", _dashboards.Dashboards.Count, filePath);
            }
            else
            {
                _dashboards = CreateDefaultDashboards();
                await SaveDashboardsAsync();
                _logger.LogInformation("Created default dashboards configuration");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboards from {FilePath}", filePath);
            _dashboards = new DashboardsFile();
        }
    }

    /// <summary>
    /// Saves dashboards configuration.
    /// </summary>
    public async Task SaveDashboardsAsync()
    {
        var filePath = GetConfigFilePath("dashboards.json");
        try
        {
            var json = JsonSerializer.Serialize(_dashboards, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved dashboards to {FilePath}", filePath);
            OnConfigurationChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving dashboards to {FilePath}", filePath);
        }
    }

    #region Connection Operations

    public ConnectionConfig? GetConnection(string id) 
        => _connections.Connections.FirstOrDefault(c => c.Id == id);

    public async Task<ConnectionConfig> AddConnectionAsync(ConnectionConfig connection)
    {
        connection.CreatedAt = DateTime.UtcNow;
        connection.UpdatedAt = DateTime.UtcNow;
        _connections.Connections.Add(connection);
        await SaveConnectionsAsync();
        return connection;
    }

    public async Task<bool> UpdateConnectionAsync(ConnectionConfig connection)
    {
        var existing = _connections.Connections.FindIndex(c => c.Id == connection.Id);
        if (existing < 0) return false;
        
        connection.UpdatedAt = DateTime.UtcNow;
        _connections.Connections[existing] = connection;
        await SaveConnectionsAsync();
        return true;
    }

    public async Task<bool> DeleteConnectionAsync(string id)
    {
        var removed = _connections.Connections.RemoveAll(c => c.Id == id) > 0;
        if (removed)
        {
            await SaveConnectionsAsync();
        }
        return removed;
    }

    #endregion

    #region Tag Operations

    public List<TagConfig> GetAllTags()
    {
        return _connections.Connections
            .SelectMany(c => c.Tags.Select(t => new { Connection = c, Tag = t }))
            .Select(x => x.Tag)
            .ToList();
    }

    public List<(ConnectionConfig Connection, TagConfig Tag)> GetAllTagsWithConnection()
    {
        return _connections.Connections
            .SelectMany(c => c.Tags.Select(t => (c, t)))
            .ToList();
    }

    public async Task<TagConfig> AddTagAsync(string connectionId, TagConfig tag)
    {
        var connection = GetConnection(connectionId);
        if (connection == null) throw new InvalidOperationException($"Connection {connectionId} not found");
        
        connection.Tags.Add(tag);
        connection.UpdatedAt = DateTime.UtcNow;
        await SaveConnectionsAsync();
        return tag;
    }

    public async Task<bool> UpdateTagAsync(string connectionId, TagConfig tag)
    {
        var connection = GetConnection(connectionId);
        if (connection == null) return false;
        
        var existing = connection.Tags.FindIndex(t => t.Id == tag.Id);
        if (existing < 0) return false;
        
        connection.Tags[existing] = tag;
        connection.UpdatedAt = DateTime.UtcNow;
        await SaveConnectionsAsync();
        return true;
    }

    public async Task<bool> DeleteTagAsync(string connectionId, string tagId)
    {
        var connection = GetConnection(connectionId);
        if (connection == null) return false;
        
        var removed = connection.Tags.RemoveAll(t => t.Id == tagId) > 0;
        if (removed)
        {
            connection.UpdatedAt = DateTime.UtcNow;
            await SaveConnectionsAsync();
        }
        return removed;
    }

    #endregion

    #region Chart Operations

    public ChartConfig? GetChart(string id) 
        => _charts.Charts.FirstOrDefault(c => c.Id == id);

    public async Task<ChartConfig> AddChartAsync(ChartConfig chart)
    {
        chart.CreatedAt = DateTime.UtcNow;
        chart.UpdatedAt = DateTime.UtcNow;
        _charts.Charts.Add(chart);
        await SaveChartsAsync();
        return chart;
    }

    public async Task<bool> UpdateChartAsync(ChartConfig chart)
    {
        var existing = _charts.Charts.FindIndex(c => c.Id == chart.Id);
        if (existing < 0) return false;
        
        chart.UpdatedAt = DateTime.UtcNow;
        _charts.Charts[existing] = chart;
        await SaveChartsAsync();
        return true;
    }

    public async Task<bool> DeleteChartAsync(string id)
    {
        var removed = _charts.Charts.RemoveAll(c => c.Id == id) > 0;
        if (removed)
        {
            await SaveChartsAsync();
        }
        return removed;
    }

    #endregion

    #region Flow Operations

    public FlowConfig? GetFlow(string id) 
        => _flows.Flows.FirstOrDefault(f => f.Id == id);

    public async Task<FlowConfig> AddFlowAsync(FlowConfig flow)
    {
        flow.CreatedAt = DateTime.UtcNow;
        flow.UpdatedAt = DateTime.UtcNow;
        _flows.Flows.Add(flow);
        await SaveFlowsAsync();
        return flow;
    }

    public async Task<bool> UpdateFlowAsync(FlowConfig flow)
    {
        var existing = _flows.Flows.FindIndex(f => f.Id == flow.Id);
        if (existing < 0) return false;
        
        flow.UpdatedAt = DateTime.UtcNow;
        _flows.Flows[existing] = flow;
        await SaveFlowsAsync();
        return true;
    }

    public async Task<bool> DeleteFlowAsync(string id)
    {
        var removed = _flows.Flows.RemoveAll(f => f.Id == id) > 0;
        if (removed)
        {
            await SaveFlowsAsync();
        }
        return removed;
    }

    #endregion

    #region Dashboard Operations

    public DashboardConfig? GetDashboard(string id) 
        => _dashboards.Dashboards.FirstOrDefault(d => d.Id == id);

    public async Task<DashboardConfig> AddDashboardAsync(DashboardConfig dashboard)
    {
        dashboard.CreatedAt = DateTime.UtcNow;
        dashboard.UpdatedAt = DateTime.UtcNow;
        _dashboards.Dashboards.Add(dashboard);
        await SaveDashboardsAsync();
        return dashboard;
    }

    public async Task<bool> UpdateDashboardAsync(DashboardConfig dashboard)
    {
        var existing = _dashboards.Dashboards.FindIndex(d => d.Id == dashboard.Id);
        if (existing < 0) return false;
        
        dashboard.UpdatedAt = DateTime.UtcNow;
        _dashboards.Dashboards[existing] = dashboard;
        await SaveDashboardsAsync();
        return true;
    }

    public async Task<bool> DeleteDashboardAsync(string id)
    {
        var removed = _dashboards.Dashboards.RemoveAll(d => d.Id == id) > 0;
        if (removed)
        {
            await SaveDashboardsAsync();
        }
        return removed;
    }

    #endregion

    private void EnsureConfigDirectoryExists()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            _logger.LogInformation("Created configuration directory: {Directory}", _configDirectory);
        }
    }

    private string GetConfigFilePath(string fileName) 
        => Path.Combine(_configDirectory, fileName);

    private ConnectionsFile CreateDefaultConnections()
    {
        return new ConnectionsFile
        {
            Connections = new List<ConnectionConfig>
            {
                new ConnectionConfig
                {
                    Id = "sim-default",
                    Name = "Default Simulator",
                    Type = "Simulator",
                    Enabled = true,
                    Tags = new List<TagConfig>
                    {
                        new TagConfig
                        {
                            Id = "sim-temp-1",
                            Name = "Temperature_001",
                            Address = "Simulator/Temperature_001",
                            DataType = "Float",
                            PollRateMs = 500,
                            Unit = "°C",
                            Description = "Simulated temperature sensor",
                            Simulator = new SimulatorSettings
                            {
                                WaveType = "Sine",
                                BaseValue = 25.0,
                                Amplitude = 10.0,
                                PeriodSeconds = 60.0,
                                NoiseLevel = 0.5
                            }
                        },
                        new TagConfig
                        {
                            Id = "sim-pressure-1",
                            Name = "Pressure_001",
                            Address = "Simulator/Pressure_001",
                            DataType = "Float",
                            PollRateMs = 500,
                            Unit = "bar",
                            Description = "Simulated pressure sensor",
                            Simulator = new SimulatorSettings
                            {
                                WaveType = "Ramp",
                                BaseValue = 5.0,
                                Amplitude = 2.0,
                                PeriodSeconds = 30.0,
                                NoiseLevel = 0.1
                            }
                        },
                        new TagConfig
                        {
                            Id = "sim-level-1",
                            Name = "Level_001",
                            Address = "Simulator/Level_001",
                            DataType = "Float",
                            PollRateMs = 1000,
                            Unit = "%",
                            Description = "Simulated level sensor",
                            Simulator = new SimulatorSettings
                            {
                                WaveType = "Triangle",
                                BaseValue = 50.0,
                                Amplitude = 30.0,
                                PeriodSeconds = 90.0,
                                NoiseLevel = 0.2
                            }
                        },
                        new TagConfig
                        {
                            Id = "sim-flow-1",
                            Name = "Flow_001",
                            Address = "Simulator/Flow_001",
                            DataType = "Float",
                            PollRateMs = 500,
                            Unit = "L/min",
                            Description = "Simulated flow sensor",
                            Simulator = new SimulatorSettings
                            {
                                WaveType = "Random",
                                BaseValue = 150.0,
                                Amplitude = 20.0,
                                PeriodSeconds = 1.0,
                                NoiseLevel = 5.0
                            }
                        },
                        new TagConfig
                        {
                            Id = "sim-status-1",
                            Name = "Status_001",
                            Address = "Simulator/Status_001",
                            DataType = "Boolean",
                            PollRateMs = 1000,
                            Description = "Simulated status indicator",
                            Simulator = new SimulatorSettings
                            {
                                WaveType = "Boolean",
                                PeriodSeconds = 15.0
                            }
                        }
                    }
                }
            }
        };
    }

    private DashboardsFile CreateDefaultDashboards()
    {
        return new DashboardsFile
        {
            Dashboards = new List<DashboardConfig>
            {
                new DashboardConfig
                {
                    Id = "dashboard-overview",
                    Name = "Process Overview",
                    Description = "Overview dashboard with key process metrics",
                    Panels = new List<DashboardPanel>
                    {
                        // Row 1: Stats
                        new DashboardPanel
                        {
                            Id = "stat-temp",
                            Title = "Temperature",
                            Type = PanelType.Stat,
                            GridX = 0, GridY = 0, GridWidth = 3, GridHeight = 2,
                            StatConfig = new StatPanelConfig
                            {
                                TagId = "sim-temp-1",
                                Label = "Temperature",
                                Unit = "°C",
                                Decimals = 1,
                                ShowSparkline = true,
                                Icon = "fa-solid fa-thermometer-half",
                                Thresholds = new() { new() { Value = 0, Color = "#22c55e" }, new() { Value = 30, Color = "#f59e0b" }, new() { Value = 35, Color = "#ef4444" } }
                            }
                        },
                        new DashboardPanel
                        {
                            Id = "stat-pressure",
                            Title = "Pressure",
                            Type = PanelType.Stat,
                            GridX = 3, GridY = 0, GridWidth = 3, GridHeight = 2,
                            StatConfig = new StatPanelConfig
                            {
                                TagId = "sim-pressure-1",
                                Label = "Pressure",
                                Unit = "bar",
                                Decimals = 2,
                                ShowSparkline = true,
                                Icon = "fa-solid fa-gauge-high",
                                Thresholds = new() { new() { Value = 0, Color = "#22c55e" }, new() { Value = 6, Color = "#f59e0b" }, new() { Value = 7, Color = "#ef4444" } }
                            }
                        },
                        new DashboardPanel
                        {
                            Id = "stat-flow",
                            Title = "Flow Rate",
                            Type = PanelType.Stat,
                            GridX = 6, GridY = 0, GridWidth = 3, GridHeight = 2,
                            StatConfig = new StatPanelConfig
                            {
                                TagId = "sim-flow-1",
                                Label = "Flow",
                                Unit = "L/min",
                                Decimals = 1,
                                ShowSparkline = true,
                                Icon = "fa-solid fa-droplet",
                                Thresholds = new() { new() { Value = 0, Color = "#3b82f6" } }
                            }
                        },
                        new DashboardPanel
                        {
                            Id = "gauge-level",
                            Title = "Tank Level",
                            Type = PanelType.Gauge,
                            GridX = 9, GridY = 0, GridWidth = 3, GridHeight = 4,
                            GaugeConfig = new GaugePanelConfig
                            {
                                TagId = "sim-level-1",
                                MinValue = 0,
                                MaxValue = 100,
                                Unit = "%",
                                GaugeType = GaugeType.Radial,
                                Thresholds = new() { new() { Value = 0, Color = "#ef4444" }, new() { Value = 20, Color = "#f59e0b" }, new() { Value = 40, Color = "#22c55e" } }
                            }
                        },
                        // Row 2: Chart
                        new DashboardPanel
                        {
                            Id = "chart-trends",
                            Title = "Process Trends",
                            Type = PanelType.Chart,
                            GridX = 0, GridY = 2, GridWidth = 9, GridHeight = 5,
                            ChartConfig = new ChartPanelConfig
                            {
                                ChartType = "Line",
                                ShowLegend = true,
                                DataSources = new()
                                {
                                    new() { TagId = "sim-temp-1", DisplayName = "Temperature", Color = "#ef4444", YAxisId = "temp-axis" },
                                    new() { TagId = "sim-pressure-1", DisplayName = "Pressure", Color = "#3b82f6", YAxisId = "pressure-axis" }
                                },
                                YAxes = new()
                                {
                                    new() { Id = "temp-axis", Name = "Temperature", Position = "Left", Color = "#ef4444", Unit = "°C" },
                                    new() { Id = "pressure-axis", Name = "Pressure", Position = "Right", Color = "#3b82f6", Unit = "bar" }
                                }
                            }
                        },
                        // Row 3: Table
                        new DashboardPanel
                        {
                            Id = "table-values",
                            Title = "All Tag Values",
                            Type = PanelType.Table,
                            GridX = 0, GridY = 7, GridWidth = 12, GridHeight = 3,
                            TableConfig = new TablePanelConfig
                            {
                                TagIds = new() { "sim-temp-1", "sim-pressure-1", "sim-level-1", "sim-flow-1", "sim-status-1" },
                                ShowTimestamp = true,
                                ShowQuality = true,
                                MaxRows = 5
                            }
                        }
                    },
                    Settings = new DashboardSettings
                    {
                        DefaultMode = TrendingMode.Realtime,
                        RefreshIntervalMs = 1000,
                        AutoRefresh = true
                    }
                }
            }
        };
    }
}
