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

            // Subscribe to config reload commands from the App
            await _mqttPublisher.SubscribeAsync(
                DataForeman.Shared.Mqtt.MqttTopics.ConfigReload, 
                "__engine__", "__reload__", qos: 1);
            // Subscribe to manual flow trigger commands from the App
            await _mqttPublisher.SubscribeAsync(
                DataForeman.Shared.Mqtt.MqttTopics.AllFlowManualTriggerWildcard, 
                "__engine__", "__manual-trigger__", qos: 1);
            _mqttPublisher.OnMessageReceived += HandleEngineCommand;

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

            // Start automatic trigger scanning (evaluates tag conditions every 500ms)
            _stateMachineService.StartScanTimer(500);

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

            _stateMachineService.StopScanTimer();
            _mqttPublisher.OnMessageReceived -= HandleEngineCommand;
            _configWatcher.Stop();
            await _pollEngine.StopAsync();
        }
    }

    /// <summary>
    /// Handles commands received from the App via MQTT (config reload, manual flow triggers).
    /// </summary>
    private async void HandleEngineCommand(string topic, string payload)
    {
        try
        {
            if (topic == DataForeman.Shared.Mqtt.MqttTopics.ConfigReload)
            {
                _logger.LogInformation("Received config reload command from App: {Payload}", payload);
                await _configService.LoadFlowsAsync();
                await _mqttFlowTriggerService.RefreshSubscriptionsAsync();
                await _flowExecutionService.RefreshFlowsAsync();
                _logger.LogInformation("Config reload completed — flows recompiled and deployment statuses published");
            }
            else if (topic.StartsWith("dataforeman/flows/") && topic.EndsWith("/trigger"))
            {
                // Extract flowId from topic: dataforeman/flows/{flowId}/trigger
                var segments = topic.Split('/');
                if (segments.Length >= 3 && !string.IsNullOrWhiteSpace(segments[2]))
                {
                    var flowId = segments[2];
                    _logger.LogInformation("Received manual trigger for flow '{FlowId}'", flowId);
                    var triggered = await _flowExecutionService.TriggerFlowAsync(flowId, "manual-ui");
                    if (!triggered)
                    {
                        _logger.LogWarning("Manual trigger failed — flow '{FlowId}' not found or not compiled", flowId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling engine command on topic '{Topic}'", topic);
        }
    }
}
