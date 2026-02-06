using System.Text.Json;
using System.Text.Json.Serialization;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// Service for managing JSON configuration files with auto-save capability.
/// </summary>
public class ConfigService
{
    private readonly string _configDirectory;
    private readonly ILogger<ConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private ConnectionsFile _connections = new();
    private ChartsFile _charts = new();
    private FlowsFile _flows = new();
    private StateMachinesFile _stateMachines = new();
    
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
    public IReadOnlyList<StateMachineConfig> StateMachines => _stateMachines.StateMachines.AsReadOnly();

    /// <summary>
    /// Loads all configuration files.
    /// </summary>
    public async Task LoadAllAsync()
    {
        await LoadConnectionsAsync();
        await LoadChartsAsync();
        await LoadFlowsAsync();
        await LoadStateMachinesAsync();

        // Post-load validation — log warnings for any config issues
        foreach (var conn in _connections.Connections)
            foreach (var w in conn.Validate())
                _logger.LogWarning("Config validation: {Warning}", w);

        foreach (var flow in _flows.Flows)
            foreach (var w in flow.Validate())
                _logger.LogWarning("Config validation: {Warning}", w);

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
    /// Loads state machine configuration.
    /// </summary>
    public async Task LoadStateMachinesAsync()
    {
        var filePath = GetConfigFilePath("state-machines.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                _stateMachines = JsonSerializer.Deserialize<StateMachinesFile>(json, _jsonOptions) ?? new StateMachinesFile();
                _logger.LogInformation("Loaded {Count} state machines from {FilePath}", _stateMachines.StateMachines.Count, filePath);
            }
            else
            {
                _stateMachines = new StateMachinesFile();
                _logger.LogInformation("No state-machines.json found, starting with empty list");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading state machines from {FilePath}", filePath);
            _stateMachines = new StateMachinesFile();
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving flows to {FilePath}", filePath);
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
        OnConfigurationChanged?.Invoke();
        return connection;
    }

    public async Task<bool> UpdateConnectionAsync(ConnectionConfig connection)
    {
        var existing = _connections.Connections.FindIndex(c => c.Id == connection.Id);
        if (existing < 0) return false;
        
        connection.UpdatedAt = DateTime.UtcNow;
        _connections.Connections[existing] = connection;
        await SaveConnectionsAsync();
        OnConfigurationChanged?.Invoke();
        return true;
    }

    public async Task<bool> DeleteConnectionAsync(string id)
    {
        var removed = _connections.Connections.RemoveAll(c => c.Id == id) > 0;
        if (removed)
        {
            await SaveConnectionsAsync();
            OnConfigurationChanged?.Invoke();
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
        OnConfigurationChanged?.Invoke();
        return chart;
    }

    public async Task<bool> UpdateChartAsync(ChartConfig chart)
    {
        var existing = _charts.Charts.FindIndex(c => c.Id == chart.Id);
        if (existing < 0) return false;
        
        chart.UpdatedAt = DateTime.UtcNow;
        _charts.Charts[existing] = chart;
        await SaveChartsAsync();
        OnConfigurationChanged?.Invoke();
        return true;
    }

    public async Task<bool> DeleteChartAsync(string id)
    {
        var removed = _charts.Charts.RemoveAll(c => c.Id == id) > 0;
        if (removed)
        {
            await SaveChartsAsync();
            OnConfigurationChanged?.Invoke();
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
        OnConfigurationChanged?.Invoke();
        return flow;
    }

    public async Task<bool> UpdateFlowAsync(FlowConfig flow)
    {
        var existing = _flows.Flows.FindIndex(f => f.Id == flow.Id);
        if (existing < 0) return false;
        
        flow.UpdatedAt = DateTime.UtcNow;
        _flows.Flows[existing] = flow;
        await SaveFlowsAsync();
        OnConfigurationChanged?.Invoke();
        return true;
    }

    public async Task<bool> DeleteFlowAsync(string id)
    {
        var removed = _flows.Flows.RemoveAll(f => f.Id == id) > 0;
        if (removed)
        {
            await SaveFlowsAsync();
            OnConfigurationChanged?.Invoke();
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
}
