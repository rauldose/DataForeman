using System.Text.Json;
using System.Text.Json.Serialization;
using DataForeman.Shared.Models;

namespace DataForeman.App.Services;

/// <summary>
/// Service for reading and writing JSON configuration files
/// </summary>
public class ConfigService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ConnectionsFile _connections = new();
    private ChartsFile _charts = new();
    private FlowsFile _flows = new();

    public event Action? OnConfigChanged;

    public ConfigService(IConfiguration configuration)
    {
        _configPath = configuration.GetValue<string>("ConfigPath") ?? Path.Combine(AppContext.BaseDirectory, "config");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        // Ensure config directory exists
        Directory.CreateDirectory(_configPath);
    }

    public async Task InitializeAsync()
    {
        await LoadAllAsync();
    }

    #region Connections

    public IReadOnlyList<ConnectionConfig> Connections => _connections.Connections.AsReadOnly();

    public ConnectionConfig? GetConnection(string id)
        => _connections.Connections.FirstOrDefault(c => c.Id == id);

    public async Task<ConnectionConfig> SaveConnectionAsync(ConnectionConfig connection)
    {
        await _lock.WaitAsync();
        try
        {
            var existing = _connections.Connections.FindIndex(c => c.Id == connection.Id);
            if (existing >= 0)
            {
                connection.UpdatedAt = DateTime.UtcNow;
                _connections.Connections[existing] = connection;
            }
            else
            {
                connection.CreatedAt = DateTime.UtcNow;
                connection.UpdatedAt = DateTime.UtcNow;
                _connections.Connections.Add(connection);
            }

            await SaveConnectionsAsync();
            OnConfigChanged?.Invoke();
            return connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteConnectionAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var removed = _connections.Connections.RemoveAll(c => c.Id == id) > 0;
            if (removed)
            {
                await SaveConnectionsAsync();
                OnConfigChanged?.Invoke();
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Charts

    public IReadOnlyList<ChartConfig> Charts => _charts.Charts.AsReadOnly();

    public ChartConfig? GetChart(string id)
        => _charts.Charts.FirstOrDefault(c => c.Id == id);

    public async Task<ChartConfig> SaveChartAsync(ChartConfig chart)
    {
        await _lock.WaitAsync();
        try
        {
            var existing = _charts.Charts.FindIndex(c => c.Id == chart.Id);
            if (existing >= 0)
            {
                chart.UpdatedAt = DateTime.UtcNow;
                _charts.Charts[existing] = chart;
            }
            else
            {
                chart.CreatedAt = DateTime.UtcNow;
                chart.UpdatedAt = DateTime.UtcNow;
                _charts.Charts.Add(chart);
            }

            await SaveChartsAsync();
            OnConfigChanged?.Invoke();
            return chart;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteChartAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var removed = _charts.Charts.RemoveAll(c => c.Id == id) > 0;
            if (removed)
            {
                await SaveChartsAsync();
                OnConfigChanged?.Invoke();
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Flows

    public IReadOnlyList<FlowConfig> Flows => _flows.Flows.AsReadOnly();

    public FlowConfig? GetFlow(string id)
        => _flows.Flows.FirstOrDefault(f => f.Id == id);

    public async Task<FlowConfig> SaveFlowAsync(FlowConfig flow)
    {
        await _lock.WaitAsync();
        try
        {
            var existing = _flows.Flows.FindIndex(f => f.Id == flow.Id);
            if (existing >= 0)
            {
                flow.UpdatedAt = DateTime.UtcNow;
                _flows.Flows[existing] = flow;
            }
            else
            {
                flow.CreatedAt = DateTime.UtcNow;
                flow.UpdatedAt = DateTime.UtcNow;
                _flows.Flows.Add(flow);
            }

            await SaveFlowsAsync();
            OnConfigChanged?.Invoke();
            return flow;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteFlowAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var removed = _flows.Flows.RemoveAll(f => f.Id == id) > 0;
            if (removed)
            {
                await SaveFlowsAsync();
                OnConfigChanged?.Invoke();
            }
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region File I/O

    private string ConnectionsPath => Path.Combine(_configPath, "connections.json");
    private string ChartsPath => Path.Combine(_configPath, "charts.json");
    private string FlowsPath => Path.Combine(_configPath, "flows.json");

    private async Task LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _connections = await LoadFileAsync<ConnectionsFile>(ConnectionsPath) ?? new ConnectionsFile();
            _charts = await LoadFileAsync<ChartsFile>(ChartsPath) ?? new ChartsFile();
            _flows = await LoadFileAsync<FlowsFile>(FlowsPath) ?? new FlowsFile();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<T?> LoadFileAsync<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading {path}: {ex.Message}");
            return null;
        }
    }

    private async Task SaveConnectionsAsync()
    {
        _connections.LastModified = DateTime.UtcNow;
        await SaveFileAsync(ConnectionsPath, _connections);
    }

    private async Task SaveChartsAsync()
    {
        _charts.LastModified = DateTime.UtcNow;
        await SaveFileAsync(ChartsPath, _charts);
    }

    private async Task SaveFlowsAsync()
    {
        _flows.LastModified = DateTime.UtcNow;
        await SaveFileAsync(FlowsPath, _flows);
    }

    private async Task SaveFileAsync<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    #endregion

    /// <summary>
    /// Get all tags across all connections (for tag selection dropdowns)
    /// </summary>
    public IEnumerable<(ConnectionConfig Connection, TagConfig Tag)> GetAllTags()
    {
        foreach (var conn in _connections.Connections)
        {
            foreach (var tag in conn.Tags)
            {
                yield return (conn, tag);
            }
        }
    }
}
