using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;

namespace DataForeman.App.Services;

/// <summary>
/// MQTT service for subscribing to real-time tag values from the Engine.
/// </summary>
public class MqttService : IAsyncDisposable
{
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;
    private volatile bool _isConnected;
    private volatile bool _isDisposing;
    private bool _isInitialized;
    private readonly object _initLock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public bool IsConnected => _isConnected;
    public event Action<bool>? OnConnectionChanged;
    public event Action<TagValueMessage>? OnTagValueReceived;
    public event Action<BulkTagValueMessage>? OnBulkTagValuesReceived;
    public event Action<ConnectionStatusMessage>? OnConnectionStatusReceived;
    public event Action<EngineStatusMessage>? OnEngineStatusReceived;

    public MqttService(IConfiguration configuration, ILogger<MqttService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Connects to the MQTT broker and subscribes to topics.
    /// </summary>
    public async Task ConnectAsync()
    {
        // Prevent multiple initialization
        lock (_initLock)
        {
            if (_isInitialized || _isDisposing) return;
            _isInitialized = true;
        }

        try
        {
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateManagedMqttClient();

            var brokerHost = _configuration.GetValue<string>("Mqtt:Host") ?? "localhost";
            var brokerPort = _configuration.GetValue<int>("Mqtt:Port", 1883);
            var clientId = _configuration.GetValue<string>("Mqtt:ClientId") ?? $"dataforeman-app-{Environment.MachineName}";

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(brokerHost, brokerPort)
                    .WithClientId(clientId)
                    .WithCleanSession(true)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                    .Build())
                .Build();

            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ConnectingFailedAsync += OnConnectingFailedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            await _mqttClient.StartAsync(options);
            _logger.LogInformation("MQTT client started, connecting to {Host}:{Port}", brokerHost, brokerPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT client");
            lock (_initLock)
            {
                _isInitialized = false;
            }
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        if (_isDisposing) return;

        _isConnected = true;
        _logger.LogInformation("Connected to MQTT broker");
        SafeInvokeConnectionChanged(true);

        try
        {
            // Subscribe to all topics
            if (_mqttClient != null)
            {
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllTagsWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllConnectionStatusWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.EngineStatus).Build() });
                _logger.LogInformation("Subscribed to MQTT topics");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to MQTT topics");
        }
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _isConnected = false;
        if (e.Exception != null && !_isDisposing)
        {
            _logger.LogWarning(e.Exception, "Disconnected from MQTT broker");
        }
        else if (!_isDisposing)
        {
            _logger.LogInformation("Disconnected from MQTT broker");
        }
        SafeInvokeConnectionChanged(false);
        return Task.CompletedTask;
    }

    private Task OnConnectingFailedAsync(ConnectingFailedEventArgs e)
    {
        if (!_isDisposing)
        {
            _logger.LogError(e.Exception, "Failed to connect to MQTT broker");
        }
        return Task.CompletedTask;
    }

    private void SafeInvokeConnectionChanged(bool connected)
    {
        try
        {
            OnConnectionChanged?.Invoke(connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectionChanged handler");
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        if (_isDisposing) return Task.CompletedTask;

        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            if (string.IsNullOrEmpty(payload)) return Task.CompletedTask;

            // Route messages based on topic
            if (topic.StartsWith("dataforeman/tags/"))
            {
                if (topic.EndsWith("/bulk"))
                {
                    var message = JsonSerializer.Deserialize<BulkTagValueMessage>(payload, _jsonOptions);
                    if (message != null)
                    {
                        SafeInvoke(() => OnBulkTagValuesReceived?.Invoke(message), "OnBulkTagValuesReceived");
                    }
                }
                else
                {
                    var message = JsonSerializer.Deserialize<TagValueMessage>(payload, _jsonOptions);
                    if (message != null)
                    {
                        SafeInvoke(() => OnTagValueReceived?.Invoke(message), "OnTagValueReceived");
                    }
                }
            }
            else if (topic.StartsWith("dataforeman/status/"))
            {
                var message = JsonSerializer.Deserialize<ConnectionStatusMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    SafeInvoke(() => OnConnectionStatusReceived?.Invoke(message), "OnConnectionStatusReceived");
                }
            }
            else if (topic == MqttTopics.EngineStatus)
            {
                var message = JsonSerializer.Deserialize<EngineStatusMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    SafeInvoke(() => OnEngineStatusReceived?.Invoke(message), "OnEngineStatusReceived");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    private void SafeInvoke(Action action, string handlerName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {HandlerName} handler", handlerName);
        }
    }

    /// <summary>
    /// Publishes a config reload command to the Engine.
    /// </summary>
    public async Task PublishConfigReloadAsync(string configType = "all")
    {
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var message = new ConfigReloadMessage
            {
                ConfigType = configType,
                Timestamp = DateTime.UtcNow
            };
            var payload = JsonSerializer.Serialize(message, _jsonOptions);

            await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(MqttTopics.ConfigReload)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

            _logger.LogInformation("Published config reload command: {ConfigType}", configType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing config reload command");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposing = true;

        if (_mqttClient != null)
        {
            // Unsubscribe from events before disposing
            _mqttClient.ConnectedAsync -= OnConnectedAsync;
            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            _mqttClient.ConnectingFailedAsync -= OnConnectingFailedAsync;
            _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceived;

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
