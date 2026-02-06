using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Messages;
using DataForeman.Shared.Models;
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
    private bool _isConnected;
    private readonly JsonSerializerOptions _jsonOptions;

    public bool IsConnected => _isConnected;
    public event Action<bool>? OnConnectionChanged;
    public event Action<TagValueMessage>? OnTagValueReceived;
    public event Action<BulkTagValueMessage>? OnBulkTagValuesReceived;
    public event Action<ConnectionStatusMessage>? OnConnectionStatusReceived;
    public event Action<EngineStatusMessage>? OnEngineStatusReceived;
    public event Action<FlowExecutionMessage>? OnFlowExecutionReceived;
    public event Action<MachineRuntimeInfo>? OnStateMachineStateReceived;
    public event Action<FlowRunSummaryMessage>? OnFlowRunSummaryReceived;
    public event Action<FlowDeploymentStatusMessage>? OnFlowDeployStatusReceived;

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

            _mqttClient.ConnectedAsync += async e =>
            {
                _isConnected = true;
                _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", brokerHost, brokerPort);
                OnConnectionChanged?.Invoke(true);

                // Subscribe to all topics
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllTagsWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllConnectionStatusWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.EngineStatus).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic("dataforeman/flows/+/execution").Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllStateMachineStateWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllFlowRunSummaryWildcard).Build() });
                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(MqttTopics.AllFlowDeployStatusWildcard).Build() });

                _logger.LogInformation("Subscribed to MQTT topics");
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

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            await _mqttClient.StartAsync(options);
            _logger.LogInformation("MQTT client started, connecting to {Host}:{Port}", brokerHost, brokerPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MQTT client");
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
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
                        OnBulkTagValuesReceived?.Invoke(message);
                    }
                }
                else
                {
                    var message = JsonSerializer.Deserialize<TagValueMessage>(payload, _jsonOptions);
                    if (message != null)
                    {
                        OnTagValueReceived?.Invoke(message);
                    }
                }
            }
            else if (topic.StartsWith("dataforeman/status/"))
            {
                var message = JsonSerializer.Deserialize<ConnectionStatusMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    OnConnectionStatusReceived?.Invoke(message);
                }
            }
            else if (topic == MqttTopics.EngineStatus)
            {
                var message = JsonSerializer.Deserialize<EngineStatusMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    OnEngineStatusReceived?.Invoke(message);
                }
            }
            else if (topic.StartsWith("dataforeman/flows/") && topic.EndsWith("/execution"))
            {
                var message = JsonSerializer.Deserialize<FlowExecutionMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    OnFlowExecutionReceived?.Invoke(message);
                }
            }
            else if (topic.StartsWith("dataforeman/statemachines/") && topic.EndsWith("/state"))
            {
                var message = JsonSerializer.Deserialize<MachineRuntimeInfo>(payload, _jsonOptions);
                if (message != null)
                {
                    OnStateMachineStateReceived?.Invoke(message);
                }
            }
            else if (topic.StartsWith("dataforeman/flows/") && topic.EndsWith("/run-summary"))
            {
                var message = JsonSerializer.Deserialize<FlowRunSummaryMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    OnFlowRunSummaryReceived?.Invoke(message);
                }
            }
            else if (topic.StartsWith("dataforeman/flows/") && topic.EndsWith("/deploy-status"))
            {
                var message = JsonSerializer.Deserialize<FlowDeploymentStatusMessage>(payload, _jsonOptions);
                if (message != null)
                {
                    OnFlowDeployStatusReceived?.Invoke(message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
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

    /// <summary>
    /// Publishes an arbitrary message to a specific MQTT topic.
    /// </summary>
    public async Task PublishMessageAsync(string topic, string payload)
    {
        if (_mqttClient == null || !_isConnected) return;

        await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());
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
