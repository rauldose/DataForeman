using System.Text.Json;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// MQTT Output node - publishes messages to an MQTT topic.
/// Receives input from upstream nodes and publishes the payload to MQTT.
/// </summary>
public class MqttOutNode : INodeRuntime
{
    public async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var topic = GetConfigString(context.Config, "topic") ?? "";
        var qos = GetConfigInt(context.Config, "qos") ?? 0;
        var retain = GetConfigBool(context.Config, "retain") ?? false;

        if (string.IsNullOrEmpty(topic))
        {
            context.Logger.Warn("MqttOutNode: No topic specified");
            return;
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

        // Publish to MQTT if publisher is available
        if (context.MqttPublisher != null)
        {
            try
            {
                await context.MqttPublisher.PublishAsync(topic, payloadString, qos, retain, ct);
                context.Logger.Info($"MQTT published to '{topic}' (QoS={qos}, Retain={retain}): {payloadString}");
            }
            catch (Exception ex)
            {
                context.Logger.Error($"Failed to publish MQTT message to '{topic}': {ex.Message}", ex);
                context.Emitter.EmitError(ex, context.Message);
                return;
            }
        }
        else
        {
            context.Logger.Warn($"MqttOutNode: No MQTT publisher available, message not sent to '{topic}'");
        }

        // Emit a derived message downstream (for chaining nodes) with proper timing and provenance
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: context.Message.Payload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );
        context.Emitter.Emit("output", outputMessage);
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
