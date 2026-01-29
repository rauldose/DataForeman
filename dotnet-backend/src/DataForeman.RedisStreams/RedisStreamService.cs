using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace DataForeman.RedisStreams;

/// <summary>
/// Interface for Redis stream operations.
/// </summary>
public interface IRedisStreamService
{
    /// <summary>
    /// Check if Redis is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Publish a telemetry message to the stream.
    /// </summary>
    Task<string> PublishTelemetryAsync(TelemetryMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a batch of telemetry messages.
    /// </summary>
    Task<int> PublishTelemetryBatchAsync(IEnumerable<TelemetryMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a flow execution message.
    /// </summary>
    Task<string> PublishFlowExecutionAsync(FlowExecutionMessage message, string streamName = "df:flows:execute", CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a consumer group if it doesn't exist.
    /// </summary>
    Task<bool> CreateConsumerGroupAsync(string streamName, string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read messages from a stream using a consumer group.
    /// </summary>
    Task<IEnumerable<StreamEntry>> ReadStreamAsync(string streamName, string groupName, string consumerName, int count = 10, int blockMs = 5000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge a message as processed.
    /// </summary>
    Task AcknowledgeAsync(string streamName, string groupName, string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending messages that haven't been acknowledged.
    /// </summary>
    Task<IEnumerable<StreamEntry>> GetPendingMessagesAsync(string streamName, string groupName, string consumerName, int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the length of a stream.
    /// </summary>
    Task<long> GetStreamLengthAsync(string streamName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trim a stream to a maximum length.
    /// </summary>
    Task TrimStreamAsync(string streamName, int maxLength, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redis Streams service implementation with reconnection and backpressure handling.
/// </summary>
public class RedisStreamService : IRedisStreamService, IAsyncDisposable
{
    private readonly RedisConnectionOptions _options;
    private readonly ILogger<RedisStreamService> _logger;
    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _backpressure;
    private volatile bool _isConnecting;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the RedisStreamService.
    /// </summary>
    public RedisStreamService(IOptions<RedisConnectionOptions> options, ILogger<RedisStreamService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _backpressure = new SemaphoreSlim(_options.ReadBatchSize * 10, _options.ReadBatchSize * 10);
    }

    /// <inheritdoc />
    public bool IsConnected => _connection?.IsConnected ?? false;

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RedisStreamService));

        if (_connection?.IsConnected == true && _database != null)
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsConnected == true && _database != null)
                return;

            if (_isConnecting)
                return;

            _isConnecting = true;

            var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
            configOptions.ClientName = _options.ClientName;
            configOptions.ConnectTimeout = _options.ConnectTimeout;
            configOptions.SyncTimeout = _options.SyncTimeout;
            configOptions.AbortOnConnectFail = _options.AbortOnConnectFail;
            configOptions.ConnectRetry = _options.ConnectRetry;

            _logger.LogInformation("Connecting to Redis at {ConnectionString}", _options.ConnectionString);

            _connection?.Dispose();
            _connection = await ConnectionMultiplexer.ConnectAsync(configOptions);
            _database = _connection.GetDatabase();

            _connection.ConnectionFailed += (sender, args) =>
            {
                _logger.LogWarning("Redis connection failed: {FailureType} - {Exception}", 
                    args.FailureType, args.Exception?.Message);
            };

            _connection.ConnectionRestored += (sender, args) =>
            {
                _logger.LogInformation("Redis connection restored to {Endpoint}", args.EndPoint);
            };

            _logger.LogInformation("Successfully connected to Redis");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis");
            throw;
        }
        finally
        {
            _isConnecting = false;
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> PublishTelemetryAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        // Backpressure handling
        await _backpressure.WaitAsync(cancellationToken);
        try
        {
            var entries = new NameValueEntry[]
            {
                new("connection_id", message.ConnectionId.ToString()),
                new("tag_id", message.TagId.ToString()),
                new("ts", message.Timestamp.ToString("O")),
                new("v", JsonSerializer.Serialize(message.Value)),
                new("q", message.Quality.ToString())
            };

            var messageId = await _database!.StreamAddAsync(
                _options.TelemetryStream,
                entries,
                maxLength: _options.MaxStreamLength,
                useApproximateMaxLength: true);

            _logger.LogDebug("Published telemetry message {MessageId} for tag {TagId}", 
                messageId, message.TagId);

            return messageId.ToString();
        }
        finally
        {
            _backpressure.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> PublishTelemetryBatchAsync(IEnumerable<TelemetryMessage> messages, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        var count = 0;

        foreach (var message in messages)
        {
            await PublishTelemetryAsync(message, cancellationToken);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<string> PublishFlowExecutionAsync(FlowExecutionMessage message, string streamName = "df:flows:execute", CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var entries = new NameValueEntry[]
        {
            new("flow_id", message.FlowId.ToString()),
            new("session_id", message.SessionId.ToString()),
            new("trigger_node_id", message.TriggerNodeId ?? ""),
            new("parameters", message.Parameters ?? "{}"),
            new("triggered_at", message.TriggeredAt.ToString("O"))
        };

        var messageId = await _database!.StreamAddAsync(streamName, entries);

        _logger.LogDebug("Published flow execution message {MessageId} for flow {FlowId}", 
            messageId, message.FlowId);

        return messageId.ToString();
    }

    /// <inheritdoc />
    public async Task<bool> CreateConsumerGroupAsync(string streamName, string groupName, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        try
        {
            // Try to create the stream and consumer group
            await _database!.StreamCreateConsumerGroupAsync(
                streamName, 
                groupName, 
                StreamPosition.NewMessages, 
                createStream: true);

            _logger.LogInformation("Created consumer group {GroupName} on stream {StreamName}", 
                groupName, streamName);
            return true;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
            _logger.LogDebug("Consumer group {GroupName} already exists on stream {StreamName}", 
                groupName, streamName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StreamEntry>> ReadStreamAsync(string streamName, string groupName, string consumerName, int count = 10, int blockMs = 5000, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var entries = await _database!.StreamReadGroupAsync(
            streamName,
            groupName,
            consumerName,
            ">",
            count,
            noAck: false);

        if (entries == null || entries.Length == 0)
            return Enumerable.Empty<StreamEntry>();

        return entries.Select(e => new StreamEntry
        {
            MessageId = e.Id.ToString(),
            StreamName = streamName,
            Data = e.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString())
        });
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(string streamName, string groupName, string messageId, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        await _database!.StreamAcknowledgeAsync(streamName, groupName, messageId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StreamEntry>> GetPendingMessagesAsync(string streamName, string groupName, string consumerName, int count = 10, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        // Read pending messages starting from the beginning
        var entries = await _database!.StreamReadGroupAsync(
            streamName,
            groupName,
            consumerName,
            "0",
            count,
            noAck: false);

        if (entries == null || entries.Length == 0)
            return Enumerable.Empty<StreamEntry>();

        return entries.Select(e => new StreamEntry
        {
            MessageId = e.Id.ToString(),
            StreamName = streamName,
            Data = e.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString())
        });
    }

    /// <inheritdoc />
    public async Task<long> GetStreamLengthAsync(string streamName, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        return await _database!.StreamLengthAsync(streamName);
    }

    /// <inheritdoc />
    public async Task TrimStreamAsync(string streamName, int maxLength, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await _database!.StreamTrimAsync(streamName, maxLength, useApproximateMaxLength: true);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _backpressure.Dispose();
        _connectionLock.Dispose();

        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}
