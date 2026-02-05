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

    public Worker(
        ILogger<Worker> logger,
        ConfigService configService,
        MqttPublisher mqttPublisher,
        MqttFlowTriggerService mqttFlowTriggerService,
        FlowExecutionService flowExecutionService,
        HistoryStore historyStore,
        PollEngine pollEngine,
        ConfigWatcher configWatcher)
    {
        _logger = logger;
        _configService = configService;
        _mqttPublisher = mqttPublisher;
        _mqttFlowTriggerService = mqttFlowTriggerService;
        _flowExecutionService = flowExecutionService;
        _historyStore = historyStore;
        _pollEngine = pollEngine;
        _configWatcher = configWatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataForeman Engine starting at: {time}", DateTimeOffset.Now);

        try
        {
            // Load configuration
            await _configService.LoadAllAsync();

            // Initialize history store
            await _historyStore.InitializeAsync();

            // Connect to MQTT broker
            await _mqttPublisher.ConnectAsync();

            // Start MQTT flow trigger service (handles mqtt-in node subscriptions)
            await _mqttFlowTriggerService.StartAsync();

            // Start flow execution service (executes flows when MQTT messages trigger them)
            await _flowExecutionService.StartAsync();

            // Start the polling engine
            await _pollEngine.StartAsync();

            // Start watching for configuration changes
            _configWatcher.Start();

            _logger.LogInformation("DataForeman Engine started successfully");

            // Wait for cancellation
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
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
