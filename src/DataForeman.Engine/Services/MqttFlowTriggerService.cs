using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// Service that manages MQTT-triggered flows.
/// Scans flows for mqtt-in nodes, subscribes to their topics,
/// and triggers flow execution when messages arrive.
/// </summary>
public class MqttFlowTriggerService : IAsyncDisposable
{
    private readonly ILogger<MqttFlowTriggerService> _logger;
    private readonly MqttPublisher _mqttPublisher;
    private readonly ConfigService _configService;
    
    // Track which flows have mqtt-in nodes: FlowId -> List of (NodeId, Topic)
    private readonly ConcurrentDictionary<string, List<MqttInNodeInfo>> _mqttInNodes = new();
    
    /// <summary>
    /// Event raised when an MQTT message triggers a flow.
    /// Parameters: flowId, nodeId, topic, payload
    /// </summary>
    public event Action<string, string, string, string>? OnFlowTriggered;

    public MqttFlowTriggerService(
        ILogger<MqttFlowTriggerService> logger,
        MqttPublisher mqttPublisher,
        ConfigService configService)
    {
        _logger = logger;
        _mqttPublisher = mqttPublisher;
        _configService = configService;
    }

    /// <summary>
    /// Starts the service by scanning flows and setting up subscriptions.
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting MQTT flow trigger service");
        
        // Subscribe to MQTT messages
        _mqttPublisher.OnMessageReceived += HandleMqttMessage;
        
        // Scan all flows for mqtt-in nodes and subscribe
        await RefreshSubscriptionsAsync();
        
        _logger.LogInformation("MQTT flow trigger service started");
    }

    /// <summary>
    /// Refreshes subscriptions by scanning flows for mqtt-in nodes.
    /// Call this when flows configuration changes.
    /// </summary>
    public async Task RefreshSubscriptionsAsync()
    {
        _logger.LogInformation("Refreshing MQTT subscriptions from flows");
        
        // Clear existing subscriptions tracking
        var oldFlowIds = _mqttInNodes.Keys.ToList();
        
        // Scan all enabled flows for mqtt-in nodes
        foreach (var flow in _configService.Flows.Where(f => f.Enabled))
        {
            var mqttInNodesInFlow = flow.Nodes
                .Where(n => n.Type == "mqtt-in")
                .ToList();

            if (mqttInNodesInFlow.Count > 0)
            {
                var nodeInfos = new List<MqttInNodeInfo>();
                
                foreach (var node in mqttInNodesInFlow)
                {
                    var topic = GetNodeProperty(node, "topic");
                    var qosStr = GetNodeProperty(node, "qos");
                    var qos = int.TryParse(qosStr, out var q) ? q : 0;

                    if (!string.IsNullOrEmpty(topic))
                    {
                        var nodeInfo = new MqttInNodeInfo
                        {
                            NodeId = node.Id,
                            Topic = topic,
                            Qos = qos
                        };
                        nodeInfos.Add(nodeInfo);

                        // Subscribe to the topic
                        await _mqttPublisher.SubscribeAsync(topic, flow.Id, node.Id, qos);
                        
                        _logger.LogInformation(
                            "Flow '{FlowName}' (id: {FlowId}) has mqtt-in node '{NodeId}' subscribing to topic '{Topic}'",
                            flow.Name, flow.Id, node.Id, topic);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Flow '{FlowName}' (id: {FlowId}) has mqtt-in node '{NodeId}' with no topic configured",
                            flow.Name, flow.Id, node.Id);
                    }
                }

                _mqttInNodes[flow.Id] = nodeInfos;
                oldFlowIds.Remove(flow.Id);
            }
            else
            {
                // Flow has no mqtt-in nodes, clear any previous subscriptions
                if (_mqttInNodes.TryRemove(flow.Id, out _))
                {
                    await _mqttPublisher.ClearFlowSubscriptionsAsync(flow.Id);
                }
                oldFlowIds.Remove(flow.Id);
            }
        }

        // Clear subscriptions for flows that no longer exist or are disabled
        foreach (var oldFlowId in oldFlowIds)
        {
            if (_mqttInNodes.TryRemove(oldFlowId, out _))
            {
                await _mqttPublisher.ClearFlowSubscriptionsAsync(oldFlowId);
                _logger.LogInformation("Cleared MQTT subscriptions for removed/disabled flow '{FlowId}'", oldFlowId);
            }
        }

        var totalSubscriptions = _mqttInNodes.Values.Sum(list => list.Count);
        _logger.LogInformation("MQTT subscription refresh complete: {Count} subscriptions across {FlowCount} flows",
            totalSubscriptions, _mqttInNodes.Count);
    }

    /// <summary>
    /// Handles incoming MQTT messages and triggers appropriate flows.
    /// </summary>
    private void HandleMqttMessage(string topic, string payload)
    {
        try
        {
            _logger.LogDebug("HandleMqttMessage called with topic '{Topic}'", topic);
            
            // Find all matching subscriptions
            var matchingSubscriptions = _mqttPublisher.GetSubscriptionsForTopic(topic).ToList();

            _logger.LogInformation("Found {MatchCount} matching subscriptions for topic '{Topic}'", 
                matchingSubscriptions.Count, topic);

            if (matchingSubscriptions.Count == 0)
            {
                // Log more details for debugging
                _logger.LogWarning("No flow subscriptions match topic '{Topic}'. Total tracked mqtt-in nodes: {NodeCount}", 
                    topic, _mqttInNodes.Values.Sum(list => list.Count));
                return;
            }

            foreach (var subscription in matchingSubscriptions)
            {
                _logger.LogInformation(
                    "MQTT message on topic '{Topic}' triggering flow '{FlowId}' node '{NodeId}'",
                    topic, subscription.FlowId, subscription.NodeId);

                // Raise event to trigger flow execution
                OnFlowTriggered?.Invoke(subscription.FlowId, subscription.NodeId, topic, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message for topic '{Topic}'", topic);
        }
    }

    /// <summary>
    /// Gets a property value from a flow node as a string.
    /// </summary>
    private static string? GetNodeProperty(FlowNode node, string propertyName)
    {
        if (node.Properties.TryGetValue(propertyName, out var value))
        {
            if (value is string strValue)
            {
                return strValue;
            }
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.GetDouble().ToString(CultureInfo.InvariantCulture),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => jsonElement.GetRawText()
                };
            }
            return value?.ToString();
        }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _mqttPublisher.OnMessageReceived -= HandleMqttMessage;
        
        // Clear all subscriptions
        foreach (var flowId in _mqttInNodes.Keys.ToList())
        {
            await _mqttPublisher.ClearFlowSubscriptionsAsync(flowId);
        }
        _mqttInNodes.Clear();
        
        _logger.LogInformation("MQTT flow trigger service disposed");
    }
}

/// <summary>
/// Information about an mqtt-in node in a flow.
/// </summary>
internal sealed class MqttInNodeInfo
{
    public required string NodeId { get; init; }
    public required string Topic { get; init; }
    public int Qos { get; init; }
}
