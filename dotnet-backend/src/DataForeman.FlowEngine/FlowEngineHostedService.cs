using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DataForeman.RedisStreams;

namespace DataForeman.FlowEngine;

/// <summary>
/// Configuration options for the flow engine service.
/// </summary>
public class FlowEngineOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "FlowEngine";

    /// <summary>
    /// Whether the flow engine is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether Redis is required for the flow engine.
    /// If false, the flow engine will work without Redis (local execution only).
    /// </summary>
    public bool RequireRedis { get; set; } = false;

    /// <summary>
    /// Consumer name for this instance.
    /// </summary>
    public string ConsumerName { get; set; } = $"flow-engine-{Environment.MachineName}";

    /// <summary>
    /// Stream name for flow execution requests.
    /// </summary>
    public string ExecutionStream { get; set; } = "df:flows:execute";

    /// <summary>
    /// Consumer group name.
    /// </summary>
    public string ConsumerGroup { get; set; } = "flow-executors";

    /// <summary>
    /// Number of messages to read per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Block timeout in milliseconds when reading from stream.
    /// </summary>
    public int BlockTimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Background service that listens for flow execution requests on Redis Streams.
/// </summary>
public class FlowEngineHostedService : BackgroundService
{
    private readonly ILogger<FlowEngineHostedService> _logger;
    private readonly IRedisStreamService? _redisService;
    private readonly IFlowExecutionEngine _executionEngine;
    private readonly FlowEngineOptions _options;
    private readonly RedisConnectionOptions _redisOptions;

    /// <summary>
    /// Initializes a new instance of the flow engine hosted service.
    /// </summary>
    public FlowEngineHostedService(
        ILogger<FlowEngineHostedService> logger,
        IRedisStreamService? redisService,
        IFlowExecutionEngine executionEngine,
        IOptions<FlowEngineOptions> options,
        IOptions<RedisConnectionOptions> redisOptions)
    {
        _logger = logger;
        _redisService = redisService;
        _executionEngine = executionEngine;
        _options = options.Value;
        _redisOptions = redisOptions.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Flow engine is disabled");
            return;
        }

        // Check if Redis is enabled
        if (!_redisOptions.Enabled || _redisService == null)
        {
            _logger.LogInformation("Flow engine running in local mode (Redis disabled). Flows will be executed on-demand without stream processing.");
            // In local mode, we don't need to listen for Redis streams
            // Flows can still be executed directly via the execution engine
            return;
        }

        _logger.LogInformation("Flow engine starting with Redis, consumer: {Consumer}", _options.ConsumerName);

        // Wait for Redis to be ready
        var retries = 0;
        while (!stoppingToken.IsCancellationRequested && retries < 10)
        {
            if (_redisService.IsConnected)
                break;

            _logger.LogInformation("Waiting for Redis connection...");
            await Task.Delay(2000, stoppingToken);
            retries++;
        }

        if (!_redisService.IsConnected)
        {
            if (_options.RequireRedis)
            {
                _logger.LogError("Failed to connect to Redis after {Retries} attempts and Redis is required", retries);
                return;
            }
            
            _logger.LogWarning("Failed to connect to Redis after {Retries} attempts. Running in local mode.", retries);
            return;
        }

        // Create consumer group if needed
        await _redisService.CreateConsumerGroupAsync(
            _options.ExecutionStream,
            _options.ConsumerGroup,
            stoppingToken);

        _logger.LogInformation("Flow engine ready, listening on stream: {Stream}", _options.ExecutionStream);

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read messages from the stream
                var messages = await _redisService.ReadStreamAsync(
                    _options.ExecutionStream,
                    _options.ConsumerGroup,
                    _options.ConsumerName,
                    _options.BatchSize,
                    _options.BlockTimeoutMs,
                    stoppingToken);

                foreach (var message in messages)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await ProcessMessageAsync(message, stoppingToken);

                    // Acknowledge the message
                    await _redisService.AcknowledgeAsync(
                        _options.ExecutionStream,
                        _options.ConsumerGroup,
                        message.MessageId,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in flow engine processing loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Flow engine stopped");
    }

    private async Task ProcessMessageAsync(StreamEntry message, CancellationToken cancellationToken)
    {
        try
        {
            // Parse the flow execution message
            if (!message.Data.TryGetValue("flow_id", out var flowIdStr) ||
                !Guid.TryParse(flowIdStr, out var flowId))
            {
                _logger.LogWarning("Invalid flow execution message: missing or invalid flow_id");
                return;
            }

            message.Data.TryGetValue("trigger_node_id", out var triggerNodeId);
            message.Data.TryGetValue("parameters", out var parametersJson);

            Dictionary<string, object?>? parameters = null;
            if (!string.IsNullOrEmpty(parametersJson))
            {
                parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersJson);
            }

            _logger.LogInformation(
                "Processing flow execution request: FlowId={FlowId}, TriggerNode={TriggerNode}",
                flowId, triggerNodeId);

            // Execute the flow
            var result = await _executionEngine.ExecuteByIdAsync(
                flowId,
                triggerNodeId,
                parameters,
                cancellationToken);

            _logger.LogInformation(
                "Flow execution completed: ExecutionId={ExecutionId}, Status={Status}, Duration={Duration}ms",
                result.ExecutionId, result.Status, result.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing flow execution message");
        }
    }
}
