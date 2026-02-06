namespace DataForeman.Engine.Services;

/// <summary>
/// File system watcher for hot-reloading configuration files.
/// </summary>
public class ConfigWatcher : IDisposable
{
    private readonly ILogger<ConfigWatcher> _logger;
    private readonly ConfigService _configService;
    private readonly PollEngine _pollEngine;
    private readonly MqttFlowTriggerService _mqttFlowTriggerService;
    private readonly FlowExecutionService _flowExecutionService;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new();
    private const int DebounceMs = 500;

    public ConfigWatcher(
        ILogger<ConfigWatcher> logger,
        ConfigService configService,
        PollEngine pollEngine,
        MqttFlowTriggerService mqttFlowTriggerService,
        FlowExecutionService flowExecutionService)
    {
        _logger = logger;
        _configService = configService;
        _pollEngine = pollEngine;
        _mqttFlowTriggerService = mqttFlowTriggerService;
        _flowExecutionService = flowExecutionService;
    }

    /// <summary>
    /// Starts watching the configuration directory for changes.
    /// </summary>
    public void Start()
    {
        if (_watcher != null) return;

        var configDirectory = _configService.ConfigDirectory;
        if (!Directory.Exists(configDirectory))
        {
            _logger.LogWarning("Config directory does not exist, cannot start watcher: {Directory}", configDirectory);
            return;
        }

        _watcher = new FileSystemWatcher(configDirectory)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Filter = "*.json",
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;

        _logger.LogInformation("Started watching configuration directory: {Directory}", configDirectory);
    }

    /// <summary>
    /// Stops watching for configuration changes.
    /// </summary>
    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _logger.LogInformation("Stopped watching configuration directory");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Configuration file changed: {FileName}", e.Name);
        DebouncedReload(e.Name);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("Configuration file renamed: {OldName} -> {NewName}", e.OldName, e.Name);
        DebouncedReload(e.Name);
    }

    private void DebouncedReload(string? fileName)
    {
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ => await ReloadConfigAsync(fileName), null, 
                TimeSpan.FromMilliseconds(DebounceMs), Timeout.InfiniteTimeSpan);
        }
    }

    private async Task ReloadConfigAsync(string? fileName)
    {
        try
        {
            _logger.LogInformation("Reloading configuration due to file change: {FileName}", fileName);

            var configType = fileName?.ToLowerInvariant() switch
            {
                "connections.json" => "connections",
                "charts.json" => "charts",
                "flows.json" => "flows",
                _ => "all"
            };

            switch (configType)
            {
                case "connections":
                    await _configService.LoadConnectionsAsync();
                    await _pollEngine.ReloadConfigurationAsync();
                    break;
                case "charts":
                    await _configService.LoadChartsAsync();
                    break;
                case "flows":
                    await _configService.LoadFlowsAsync();
                    await _mqttFlowTriggerService.RefreshSubscriptionsAsync();
                    await _flowExecutionService.RefreshFlowsAsync();
                    break;
                default:
                    await _configService.LoadAllAsync();
                    await _pollEngine.ReloadConfigurationAsync();
                    await _mqttFlowTriggerService.RefreshSubscriptionsAsync();
                    await _flowExecutionService.RefreshFlowsAsync();
                    break;
            }

            _logger.LogInformation("Configuration reloaded successfully: {ConfigType}", configType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading configuration");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
