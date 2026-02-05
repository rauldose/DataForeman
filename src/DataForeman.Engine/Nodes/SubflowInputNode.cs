using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// Entry point for data into a subflow.
/// Receives input data from the parent flow context and passes it to subflow nodes.
/// </summary>
public class SubflowInputNode : INodeRuntime
{
    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Pass through the incoming message to the subflow
        // The message comes from the parent flow's subflow node
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: context.Message.Payload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );

        context.Logger.Info($"Subflow input received: {context.Message.Payload}");
        context.Emitter.Emit("output", outputMessage);

        return ValueTask.CompletedTask;
    }
}
