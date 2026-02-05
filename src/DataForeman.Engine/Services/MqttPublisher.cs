using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace DataForeman.Engine.Services;

/// <summary>
/// MQTT publisher and subscriber service for sending/receiving real-time messages.
/// </summary>
public class MqttPublisher : IAsyncDisposable
{
    private readonly ILogger<MqttPublisher> _logger;
    private readonly IConfiguration _configuration;
    private IManagedMqttClient? _mqttClient;
    private bool _isConnected;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, List<MqttSubscriptionInfo>> _subscriptions = new();

    public bool IsConnected => _isConnected;
    public event Action<bool>? OnConnectionChanged;
    
    /// <summary>
    /// Event raised when a message is received on a subscribed topic.
    /// </summary>
    public event Action<string, string>? OnMessageReceived;

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
                
                // Re-subscribe to all topics when reconnected
                await ResubscribeAllAsync();
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

            _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceivedAsync;

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
        await PublishMessageAsync(topic, payload, qos: 0, retain: false);
    }

    /// <summary>
    /// Publishes a raw message to a topic with QoS and retain options.
    /// </summary>
    public async Task PublishMessageAsync(string topic, string payload, int qos, bool retain)
    {
        if (_mqttClient == null || !_isConnected) return;

        try
        {
            var qosLevel = qos switch
            {
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtMostOnce
            };

            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qosLevel);

            if (retain)
            {
                builder.WithRetainFlag(true);
            }

            await _mqttClient.EnqueueAsync(builder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to {Topic}", topic);
        }
    }

    /// <summary>
    /// Subscribes to an MQTT topic with associated metadata.
    /// </summary>
    /// <param name="topic">The MQTT topic to subscribe to</param>
    /// <param name="flowId">The flow ID associated with this subscription</param>
    /// <param name="nodeId">The mqtt-in node ID that requires this subscription</param>
    /// <param name="qos">Quality of service level</param>
    public async Task SubscribeAsync(string topic, string flowId, string nodeId, int qos = 0)
    {
        if (string.IsNullOrEmpty(topic))
        {
            _logger.LogWarning("Cannot subscribe to empty topic");
            return;
        }

        var subscriptionInfo = new MqttSubscriptionInfo
        {
            Topic = topic,
            FlowId = flowId,
            NodeId = nodeId,
            Qos = qos
        };

        // Add to subscription tracking
        // Note: flowId and nodeId are GUIDs and cannot contain colons, making this key format safe
        var key = $"{flowId}:{nodeId}";
        _subscriptions.AddOrUpdate(
            key,
            _ => new List<MqttSubscriptionInfo> { subscriptionInfo },
            (_, list) =>
            {
                if (!list.Any(s => s.Topic == topic))
                {
                    list.Add(subscriptionInfo);
                }
                return list;
            });

        // Subscribe if connected
        if (_mqttClient != null && _isConnected)
        {
            try
            {
                var qosLevel = qos switch
                {
                    1 => MqttQualityOfServiceLevel.AtLeastOnce,
                    2 => MqttQualityOfServiceLevel.ExactlyOnce,
                    _ => MqttQualityOfServiceLevel.AtMostOnce
                };

                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter>
                {
                    new MqttTopicFilterBuilder()
                        .WithTopic(topic)
                        .WithQualityOfServiceLevel(qosLevel)
                        .Build()
                });

                _logger.LogInformation("Subscribed to MQTT topic '{Topic}' for flow '{FlowId}' node '{NodeId}'", 
                    topic, flowId, nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to topic '{Topic}'", topic);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from all topics associated with a flow/node.
    /// </summary>
    public async Task UnsubscribeAsync(string flowId, string nodeId)
    {
        var key = $"{flowId}:{nodeId}";
        if (_subscriptions.TryRemove(key, out var subscriptions) && _mqttClient != null && _isConnected)
        {
            foreach (var sub in subscriptions)
            {
                try
                {
                    // Check if any other subscription uses this topic
                    var topicStillNeeded = _subscriptions.Values
                        .SelectMany(list => list)
                        .Any(s => s.Topic == sub.Topic);

                    if (!topicStillNeeded)
                    {
                        await _mqttClient.UnsubscribeAsync(sub.Topic);
                        _logger.LogInformation("Unsubscribed from MQTT topic '{Topic}'", sub.Topic);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unsubscribing from topic '{Topic}'", sub.Topic);
                }
            }
        }
    }

    /// <summary>
    /// Clears all subscriptions for a specific flow.
    /// </summary>
    public async Task ClearFlowSubscriptionsAsync(string flowId)
    {
        var keysToRemove = _subscriptions.Keys.Where(k => k.StartsWith($"{flowId}:")).ToList();
        foreach (var key in keysToRemove)
        {
            var parts = key.Split(':');
            if (parts.Length >= 2)
            {
                await UnsubscribeAsync(parts[0], parts[1]);
            }
        }
    }

    /// <summary>
    /// Gets the subscription info for a specific topic match.
    /// </summary>
    public IEnumerable<MqttSubscriptionInfo> GetSubscriptionsForTopic(string topic)
    {
        return _subscriptions.Values
            .SelectMany(list => list)
            .Where(sub => TopicMatches(sub.Topic, topic));
    }

    /// <summary>
    /// Re-subscribes to all tracked topics after reconnection.
    /// </summary>
    private async Task ResubscribeAllAsync()
    {
        if (_mqttClient == null || !_isConnected) return;

        var allTopics = _subscriptions.Values
            .SelectMany(list => list)
            .Select(s => (s.Topic, s.Qos))
            .Distinct()
            .ToList();

        foreach (var (topic, qos) in allTopics)
        {
            try
            {
                var qosLevel = qos switch
                {
                    1 => MqttQualityOfServiceLevel.AtLeastOnce,
                    2 => MqttQualityOfServiceLevel.ExactlyOnce,
                    _ => MqttQualityOfServiceLevel.AtMostOnce
                };

                await _mqttClient.SubscribeAsync(new List<MqttTopicFilter>
                {
                    new MqttTopicFilterBuilder()
                        .WithTopic(topic)
                        .WithQualityOfServiceLevel(qosLevel)
                        .Build()
                });

                _logger.LogDebug("Re-subscribed to MQTT topic '{Topic}'", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-subscribing to topic '{Topic}'", topic);
            }
        }

        _logger.LogInformation("Re-subscribed to {Count} MQTT topics after reconnection", allTopics.Count);
    }

    /// <summary>
    /// Handles incoming MQTT messages.
    /// </summary>
    private Task HandleMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            _logger.LogDebug("MQTT message received on topic '{Topic}': {Payload}", topic, payload);

            // Notify subscribers
            OnMessageReceived?.Invoke(topic, payload ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if an MQTT topic matches a topic filter (supports + and # wildcards).
    /// </summary>
    private static bool TopicMatches(string filter, string topic)
    {
        if (filter == topic) return true;
        if (filter == "#") return true;

        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');

        for (int i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "#")
            {
                return true; // # matches everything from here on
            }

            if (i >= topicParts.Length)
            {
                return false; // More filter parts than topic parts
            }

            if (filterParts[i] != "+" && filterParts[i] != topicParts[i])
            {
                return false; // Not a wildcard and doesn't match
            }
        }

        return filterParts.Length == topicParts.Length;
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

/// <summary>
/// Information about an MQTT subscription for a flow node.
/// </summary>
public sealed class MqttSubscriptionInfo
{
    /// <summary>The MQTT topic filter.</summary>
    public required string Topic { get; init; }
    
    /// <summary>The flow ID that owns this subscription.</summary>
    public required string FlowId { get; init; }
    
    /// <summary>The mqtt-in node ID.</summary>
    public required string NodeId { get; init; }
    
    /// <summary>Quality of service level.</summary>
    public int Qos { get; init; }
}
