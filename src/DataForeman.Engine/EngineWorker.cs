using System.Diagnostics;
using DataForeman.Engine.Services;
using DataForeman.Shared.Models;

namespace DataForeman.Engine;

/// <summary>
/// Main worker service that orchestrates the engine components
/// </summary>
public class EngineWorker : BackgroundService
{
    private readonly ILogger<EngineWorker> _logger;
    private readonly ConfigWatcher _config;
    private readonly MqttPublisher _mqtt;
    private readonly PollEngine _poll;
    private readonly HistoryStore _history;
    private Timer? _statusTimer;
    private readonly Process _process;

    public EngineWorker(
        ILogger<EngineWorker> logger,
        ConfigWatcher config,
        MqttPublisher mqtt,
        PollEngine poll,
        HistoryStore history)
    {
        _logger = logger;
        _config = config;
        _mqtt = mqtt;
        _poll = poll;
        _history = history;
        _process = Process.GetCurrentProcess();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataForeman Engine starting...");

        // Initialize components
        _history.Initialize();
        _config.Initialize();

        // Connect to MQTT
        await _mqtt.ConnectAsync();

        // Subscribe to events
        _config.OnConnectionsChanged += OnConnectionsChanged;
        _mqtt.OnCommand += OnCommandReceived;
        _mqtt.OnHistoryRequest += OnHistoryRequestReceived;

        // Load initial config
        if (_config.Connections != null)
        {
            _poll.LoadConfig(_config.Connections);
        }

        // Start status publishing timer (every 1 second)
        _statusTimer = new Timer(_ => PublishStatus(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        _logger.LogInformation("DataForeman Engine started");

        // Keep running until cancellation
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("DataForeman Engine stopping...");

        _statusTimer?.Dispose();
        _poll.Stop();
    }

    private void OnConnectionsChanged(ConnectionsFile config)
    {
        _logger.LogInformation("Reloading connections configuration");
        _poll.LoadConfig(config);
    }

    private async Task OnCommandReceived(EngineCommandMessage cmd)
    {
        _logger.LogInformation("Received command: {Command}", cmd.Command);

        switch (cmd.Command)
        {
            case EngineCommand.ReloadConfig:
                _config.LoadAll();
                if (_config.Connections != null)
                    _poll.LoadConfig(_config.Connections);
                break;

            case EngineCommand.Shutdown:
                _logger.LogWarning("Shutdown command received");
                Environment.Exit(0);
                break;
        }

        await Task.CompletedTask;
    }

    private async Task OnHistoryRequestReceived(HistoryRequestMessage request)
    {
        _logger.LogDebug("History request for {Connection}/{Tag}", request.ConnectionId, request.TagId);

        var points = _history.Query(
            request.ConnectionId,
            request.TagId,
            request.StartTime,
            request.EndTime,
            request.MaxPoints
        );

        var response = new TagHistoryMessage
        {
            ConnectionId = request.ConnectionId,
            TagId = request.TagId,
            Points = points
        };

        await _mqtt.PublishHistoryAsync(response);
    }

    private void PublishStatus()
    {
        try
        {
            _process.Refresh();

            var status = new EngineStatusMessage
            {
                Running = true,
                ActiveConnections = _poll.ActiveConnections,
                ActiveTags = _poll.ActiveTags,
                ActiveFlows = 0,  // TODO: Implement flow tracking
                CpuUsage = 0,  // TODO: Calculate CPU usage
                MemoryUsedBytes = _process.WorkingSet64,
                ScanCount = _poll.ScanCount,
                AverageScanTimeMs = _poll.AverageScanTimeMs,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _mqtt.PublishStatusAsync(status).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing status");
        }
    }
}
