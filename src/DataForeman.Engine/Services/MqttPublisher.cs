using System.Text.Json;
using DataForeman.Shared.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace DataForeman.Engine.Services;

/// <summary>
/// MQTT publisher service for sending real-time tag values.
/// </summary>
public class MqttPublisher : IAsyncDisposable
{
    private readonly ILogger<MqttPublisher> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;
    private bool _isConnected;
    private readonly JsonSerializerOptions _jsonOptions;

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

            _mqttClient.ConnectedAsync += async e =>
            {
                _isConnected = true;
                _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", brokerHost, brokerPort);
                OnConnectionChanged?.Invoke(true);
                await Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _isConnected = false;
                if (e.Exception != null)
                {
                    _logger.LogWarning(e.Exception, "Disconnected from MQTT broker");
                }
                else
                {
                    _logger.LogInformation("Disconnected from MQTT broker");
                }
                OnConnectionChanged?.Invoke(false);
                await Task.CompletedTask;
            };

            _mqttClient.ConnectingFailedAsync += async e =>
            {
                _logger.LogError(e.Exception, "Failed to connect to MQTT broker");
                await Task.CompletedTask;
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
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var topic = MqttTopics.GetTagValueTopic(message.ConnectionId, message.TagId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .WithRetainFlag(true)
                .Build());
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
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var topic = MqttTopics.GetBulkTagValueTopic(message.ConnectionId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build());
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
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var topic = MqttTopics.GetConnectionStatusTopic(message.ConnectionId);
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build());
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
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(MqttTopics.EngineStatus)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing engine status");
        }
    }

    /// <summary>
    /// Publishes a raw message to a topic.
    /// </summary>
    public async Task PublishMessageAsync(string topic, string payload)
    {
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to {Topic}", topic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync();
            _mqttClient.Dispose();
            _logger.LogInformation("MQTT client disposed");
        }
    }
}
