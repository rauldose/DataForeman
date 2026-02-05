using DataForeman.Shared.Runtime;
using System.Text.Json;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// Exit point for data from a subflow.
/// Collects output data from subflow processing and returns it to the parent flow.
/// </summary>
public class SubflowOutputNode : INodeRuntime
{
    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Get the output name from config (allows multiple named outputs)
        var config = context.Config;
        var outputName = "output";
        if (config.HasValue && config.Value.TryGetProperty("outputName", out var nameElement))
        {
            outputName = nameElement.GetString() ?? "output";
        }

        // Add metadata indicating this is a subflow output
        var additionalHeaders = new Dictionary<string, string>
        {
            ["isSubflowOutput"] = "true",
            ["outputName"] = outputName
        };

        // Emit the message with subflow output metadata
        // This will be captured by the parent flow's subflow node
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: context.Message.Payload,
            additionalHeaders: additionalHeaders,
            sourceNodeId: context.Node.Id,
            sourcePort: outputName
        );

        context.Logger.Info($"Subflow output '{outputName}': {context.Message.Payload}");
        context.Emitter.Emit(outputName, outputMessage);

        return ValueTask.CompletedTask;
    }
}
