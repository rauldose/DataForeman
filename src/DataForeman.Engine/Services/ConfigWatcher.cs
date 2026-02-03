using System.Text.Json;
using System.Text.Json.Serialization;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// Watches configuration files for changes
/// </summary>
public class ConfigWatcher : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigWatcher> _logger;
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private FileSystemWatcher? _watcher;

    public event Action<ConnectionsFile>? OnConnectionsChanged;
    public event Action<FlowsFile>? OnFlowsChanged;

    public ConnectionsFile? Connections { get; private set; }
    public FlowsFile? Flows { get; private set; }

    public ConfigWatcher(IConfiguration configuration, ILogger<ConfigWatcher> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configPath = _configuration.GetValue<string>("ConfigPath") ?? Path.Combine(AppContext.BaseDirectory, "config");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        Directory.CreateDirectory(_configPath);
    }

    public void Initialize()
    {
        LoadAll();
        StartWatching();
    }

    public void LoadAll()
    {
        LoadConnections();
        LoadFlows();
    }

    private void LoadConnections()
    {
        var path = Path.Combine(_configPath, "connections.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                Connections = JsonSerializer.Deserialize<ConnectionsFile>(json, _jsonOptions);
                _logger.LogInformation("Loaded {Count} connections from config",
                    Connections?.Connections.Count ?? 0);
                OnConnectionsChanged?.Invoke(Connections ?? new ConnectionsFile());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load connections.json");
            }
        }
        else
        {
            Connections = new ConnectionsFile();
        }
    }

    private void LoadFlows()
    {
        var path = Path.Combine(_configPath, "flows.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                Flows = JsonSerializer.Deserialize<FlowsFile>(json, _jsonOptions);
                _logger.LogInformation("Loaded {Count} flows from config",
                    Flows?.Flows.Count ?? 0);
                OnFlowsChanged?.Invoke(Flows ?? new FlowsFile());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load flows.json");
            }
        }
        else
        {
            Flows = new FlowsFile();
        }
    }

    private void StartWatching()
    {
        _watcher = new FileSystemWatcher(_configPath, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching for config changes in {Path}", _configPath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid changes
        Task.Delay(100).ContinueWith(_ =>
        {
            var fileName = Path.GetFileName(e.FullPath);
            _logger.LogInformation("Config file changed: {File}", fileName);

            if (fileName == "connections.json")
                LoadConnections();
            else if (fileName == "flows.json")
                LoadFlows();
        });
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
