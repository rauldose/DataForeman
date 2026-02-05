using System.Text.Json;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// MQTT Input node - subscribes to an MQTT topic and triggers flow on message receipt.
/// This is a trigger node that starts flow execution when MQTT messages arrive.
/// </summary>
public class MqttInNode : INodeRuntime
{
    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // MqttInNode is a trigger node - it receives messages from MQTT subscription
        // When an MQTT message arrives, this node is triggered and passes the payload downstream
        
        var topic = GetConfigString(context.Config, "topic") ?? "#";
        
        // Create output message with the incoming payload
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: context.Message.Payload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );

        context.Logger.Info($"MQTT message received on topic '{topic}': {context.Message.Payload}");
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
}
