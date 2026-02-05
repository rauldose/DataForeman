using System.Text.Json;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// MQTT Output node - publishes messages to an MQTT topic.
/// Receives input from upstream nodes and publishes the payload to MQTT.
/// </summary>
public class MqttOutNode : INodeRuntime
{
    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var topic = GetConfigString(context.Config, "topic") ?? "";
        var qos = GetConfigInt(context.Config, "qos") ?? 0;
        var retain = GetConfigBool(context.Config, "retain") ?? false;

        if (string.IsNullOrEmpty(topic))
        {
            context.Logger.Warn("MqttOutNode: No topic specified");
            return ValueTask.CompletedTask;
        }

        // Serialize the payload
        string payloadString;
        var payload = context.Message.Payload;
        if (payload.HasValue)
        {
            if (payload.Value.ValueKind == JsonValueKind.String)
            {
                payloadString = payload.Value.GetString() ?? "";
            }
            else
            {
                payloadString = payload.Value.GetRawText();
            }
        }
        else
        {
            payloadString = "";
        }

        // Log the publish action - actual MQTT publishing is handled by the engine
        context.Logger.Info($"MQTT publish to '{topic}' (QoS={qos}, Retain={retain}): {payloadString}");
        
        // Store the publish request in payload for the engine to process
        var publishData = new Dictionary<string, object>
        {
            ["mqttPublish"] = true,
            ["topic"] = topic,
            ["payload"] = payloadString,
            ["qos"] = qos,
            ["retain"] = retain
        };
        
        var jsonPayload = MessageEnvelope.CreatePayload(publishData);
        
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: jsonPayload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );

        context.Emitter.Emit("output", outputMessage);

        return ValueTask.CompletedTask;
    }
    
    private static string? GetConfigString(JsonElement? config, string propertyName)
    {
        if (config?.ValueKind == JsonValueKind.Object && 
            config.Value.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }
    
    private static int? GetConfigInt(JsonElement? config, string propertyName)
    {
        if (config?.ValueKind == JsonValueKind.Object && 
            config.Value.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return null;
    }
    
    private static bool? GetConfigBool(JsonElement? config, string propertyName)
    {
        if (config?.ValueKind == JsonValueKind.Object && 
            config.Value.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }
}
