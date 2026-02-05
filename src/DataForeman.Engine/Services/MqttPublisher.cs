using System.Text.Json;
using DataForeman.Shared.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace DataForeman.Engine.Services;

/// <summary>
/// MQTT publisher service for sending real-time tag values with connection state synchronization.
/// </summary>
public class MqttPublisher : IAsyncDisposable
{
    private readonly ILogger<MqttPublisher> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;
    private volatile bool _isConnected;
    private volatile bool _isDisposing;
    private readonly object _connectionLock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    // Retry configuration
    private const int MaxPublishRetries = 3;
    private const int RetryDelayMs = 100;

    public bool IsConnected => _isConnected;
    public event Action<bool>? OnConnectionChanged;

    public MqttPublisher(IConfiguration configuration, ILogger<MqttPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Connects to the MQTT broker.
    /// </summary>
    public async Task ConnectAsync()
    {
        try
        {
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateManagedMqttClient();

            var brokerHost = _configuration.GetValue<string>("Mqtt:Host") ?? "localhost";
            var brokerPort = _configuration.GetValue<int>("Mqtt:Port", 1883);
            var clientId = _configuration.GetValue<string>("Mqtt:ClientId") ?? $"dataforeman-engine-{Environment.MachineName}";

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(brokerHost, brokerPort)
                    .WithClientId(clientId)
                    .WithCleanSession(true)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                    .Build())
                .Build();

            _mqttClient.ConnectedAsync += e =>
            {
                lock (_connectionLock)
                {
                    _isConnected = true;
                }
                _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", brokerHost, brokerPort);
                try
                {
                    OnConnectionChanged?.Invoke(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnConnectionChanged handler");
                }
                return Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += e =>
            {
                lock (_connectionLock)
                {
                    _isConnected = false;
                }
                if (e.Exception != null)
                {
                    _logger.LogWarning(e.Exception, "Disconnected from MQTT broker");
                }
                else
                {
                    _logger.LogInformation("Disconnected from MQTT broker");
                }
                try
                {
                    OnConnectionChanged?.Invoke(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnConnectionChanged handler");
                }
                return Task.CompletedTask;
            };

            _mqttClient.ConnectingFailedAsync += e =>
            {
                _logger.LogError(e.Exception, "Failed to connect to MQTT broker");
                return Task.CompletedTask;
            };

            await _mqttClient.StartAsync(options);
            _logger.LogInformation("MQTT client started, connecting to {Host}:{Port}", brokerHost, brokerPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT client");
        }
    }

    /// <summary>
    /// Publishes a single tag value.
    /// </summary>
    public async Task PublishTagValueAsync(TagValueMessage message)
    {
        if (!CanPublish()) return;

        try
        {
            var topic = MqttTopics.GetTagValueTopic(message.ConnectionId, message.TagId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await EnqueueWithRetryAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .WithRetainFlag(true)
                    .Build(),
                $"tag value for {message.TagId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing tag value for {TagId}", message.TagId);
        }
    }

    /// <summary>
    /// Publishes bulk tag values for a connection.
    /// </summary>
    public async Task PublishBulkTagValuesAsync(BulkTagValueMessage message)
    {
        if (!CanPublish()) return;

        try
        {
            var topic = MqttTopics.GetBulkTagValueTopic(message.ConnectionId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await EnqueueWithRetryAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build(),
                $"bulk tag values for connection {message.ConnectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing bulk tag values for connection {ConnectionId}", message.ConnectionId);
        }
    }

    /// <summary>
    /// Publishes connection status.
    /// </summary>
    public async Task PublishConnectionStatusAsync(ConnectionStatusMessage message)
    {
        if (!CanPublish()) return;

        try
        {
            var topic = MqttTopics.GetConnectionStatusTopic(message.ConnectionId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await EnqueueWithRetryAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(true)
                    .Build(),
                $"connection status for {message.ConnectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing connection status for {ConnectionId}", message.ConnectionId);
        }
    }

    /// <summary>
    /// Publishes engine status.
    /// </summary>
    public async Task PublishEngineStatusAsync(EngineStatusMessage message)
    {
        if (!CanPublish()) return;

        try
        {
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await EnqueueWithRetryAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic(MqttTopics.EngineStatus)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(true)
                    .Build(),
                "engine status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing engine status");
        }
    }

    /// <summary>
    /// Checks if publishing is possible (thread-safe).
    /// </summary>
    private bool CanPublish()
    {
        if (_isDisposing) return false;

        lock (_connectionLock)
        {
            return _mqttClient != null && _isConnected;
        }
    }

    /// <summary>
    /// Enqueues a message with retry logic.
    /// </summary>
    private async Task EnqueueWithRetryAsync(MqttApplicationMessage message, string description)
    {
        for (var attempt = 1; attempt <= MaxPublishRetries; attempt++)
        {
            if (_isDisposing) return;

            try
            {
                // Re-check connection state before each attempt
                if (!CanPublish())
                {
                    _logger.LogDebug("Cannot publish {Description} - not connected (attempt {Attempt})", description, attempt);
                    if (attempt < MaxPublishRetries)
                    {
                        await Task.Delay(RetryDelayMs);
                    }
                    continue;
                }

                await _mqttClient!.EnqueueAsync(message);
                return; // Success
            }
            catch (Exception ex) when (attempt < MaxPublishRetries)
            {
                _logger.LogDebug(ex, "Failed to enqueue {Description} (attempt {Attempt}/{MaxRetries})",
                    description, attempt, MaxPublishRetries);
                await Task.Delay(RetryDelayMs * attempt); // Exponential backoff
            }
        }

        _logger.LogWarning("Failed to publish {Description} after {MaxRetries} attempts", description, MaxPublishRetries);
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposing = true;

        if (_mqttClient != null)
        {
            try
            {
                await _mqttClient.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping MQTT client");
            }

            _mqttClient.Dispose();
            _logger.LogInformation("MQTT client disposed");
        }
    }
}
