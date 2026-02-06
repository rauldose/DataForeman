using DataForeman.Engine.Services;

namespace DataForeman.Engine;

/// <summary>
/// Background worker service that manages the polling engine lifecycle.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ConfigService _configService;
    private readonly MqttPublisher _mqttPublisher;
    private readonly MqttFlowTriggerService _mqttFlowTriggerService;
    private readonly FlowExecutionService _flowExecutionService;
    private readonly HistoryStore _historyStore;
    private readonly PollEngine _pollEngine;
    private readonly ConfigWatcher _configWatcher;
    private readonly StateMachineExecutionService _stateMachineService;
    private readonly EngineHealthMonitor _healthMonitor;

    public Worker(
        ILogger<Worker> logger,
        ConfigService configService,
        MqttPublisher mqttPublisher,
        MqttFlowTriggerService mqttFlowTriggerService,
        FlowExecutionService flowExecutionService,
        HistoryStore historyStore,
        PollEngine pollEngine,
        ConfigWatcher configWatcher,
        StateMachineExecutionService stateMachineService,
        EngineHealthMonitor healthMonitor)
    {
        _logger = logger;
        _configService = configService;
        _mqttPublisher = mqttPublisher;
        _mqttFlowTriggerService = mqttFlowTriggerService;
        _flowExecutionService = flowExecutionService;
        _historyStore = historyStore;
        _pollEngine = pollEngine;
        _configWatcher = configWatcher;
        _stateMachineService = stateMachineService;
        _healthMonitor = healthMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataForeman Engine starting at: {time}", DateTimeOffset.Now);

        try
        {
            // Load configuration
            await _configService.LoadAllAsync();
            _healthMonitor.SetConfigLoaded(true);

            // Initialize history store
            await _historyStore.InitializeAsync();

            // Connect to MQTT broker
            await _mqttPublisher.ConnectAsync();
            _mqttPublisher.OnConnectionChanged += connected =>
                _healthMonitor.SetMqttConnected(connected);

            // Start MQTT flow trigger service (handles mqtt-in node subscriptions)
            await _mqttFlowTriggerService.StartAsync();

            // Start flow execution service (executes flows when MQTT messages trigger them)
            await _flowExecutionService.StartAsync();

            // Load and start state machines, publishing runtime state via MQTT
            _stateMachineService.ReloadAll(_configService.StateMachines);
            _healthMonitor.SetLoadedStateMachineCount(
                _stateMachineService.GetAllRuntimeInfo().Count);
            _stateMachineService.RuntimeInfoUpdated += async info =>
            {
                try { await _mqttPublisher.PublishStateMachineStateAsync(info); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to publish state machine state for {Id}", info.ConfigId); }
            };
            // Publish initial state snapshots for all loaded machines
            var initSnapshots = _stateMachineService.GetAllRuntimeInfo();
            foreach (var snapshot in initSnapshots)
            {
                await _mqttPublisher.PublishStateMachineStateAsync(snapshot);
            }

            // Start the polling engine
            await _pollEngine.StartAsync();
            _healthMonitor.SetPollEngineRunning(true);

            // Start watching for configuration changes
            _configWatcher.Start();

            _logger.LogInformation("DataForeman Engine started successfully");

            // Periodic health check loop
            while (!stoppingToken.IsCancellationRequested)
            {
                _healthMonitor.LogHealthStatus();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DataForeman Engine worker");
        }
        finally
        {
            _logger.LogInformation("DataForeman Engine shutting down at: {time}", DateTimeOffset.Now);

            _configWatcher.Stop();
            await _pollEngine.StopAsync();
        }
    }
}
