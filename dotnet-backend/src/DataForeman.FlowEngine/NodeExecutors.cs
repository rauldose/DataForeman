namespace DataForeman.FlowEngine;

/// <summary>
/// Interface for flow node executors.
/// </summary>
public interface INodeExecutor
{
    /// <summary>
    /// Node type this executor handles.
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// Execute the node.
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context);
}

/// <summary>
/// Base class for node executors with common functionality.
/// </summary>
public abstract class NodeExecutorBase : INodeExecutor
{
    /// <inheritdoc />
    public abstract string NodeType { get; }

    /// <inheritdoc />
    public abstract Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context);

    /// <summary>
    /// Get a configuration value from the node.
    /// </summary>
    protected T? GetConfig<T>(FlowNode node, string key)
    {
        if (node.Config?.TryGetValue(key, out var element) == true)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    /// <summary>
    /// Get an input value from a connected node.
    /// </summary>
    protected object? GetInput(FlowNode node, FlowExecutionContext context, string inputName)
    {
        // The input name maps to the source node ID
        var inputNodeId = GetConfig<string>(node, $"input_{inputName}");
        if (!string.IsNullOrEmpty(inputNodeId))
        {
            return context.GetNodeOutput<object>(inputNodeId);
        }
        return null;
    }
}

/// <summary>
/// Manual trigger node executor.
/// </summary>
public class ManualTriggerExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "trigger-manual";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        // Manual trigger just passes through
        return Task.FromResult(NodeExecutionResult.Ok(new { triggered = true, timestamp = DateTime.UtcNow }));
    }
}

/// <summary>
/// Schedule trigger node executor.
/// </summary>
public class ScheduleTriggerExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "trigger-schedule";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var schedule = GetConfig<string>(node, "schedule") ?? "* * * * *";
        return Task.FromResult(NodeExecutionResult.Ok(new { triggered = true, schedule, timestamp = DateTime.UtcNow }));
    }
}

/// <summary>
/// Tag input node executor.
/// </summary>
public class TagInputExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "tag-input";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var tagId = GetConfig<int>(node, "tagId");
        var tagPath = GetConfig<string>(node, "tagPath");

        // TODO: Read actual tag value from driver or cache
        var value = 0.0; // Simulated

        return Task.FromResult(NodeExecutionResult.Ok(new { tagId, tagPath, value, timestamp = DateTime.UtcNow }));
    }
}

/// <summary>
/// Tag output node executor.
/// </summary>
public class TagOutputExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "tag-output";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var tagId = GetConfig<int>(node, "tagId");
        var tagPath = GetConfig<string>(node, "tagPath");
        var inputValue = GetInput(node, context, "value");

        // TODO: Write value to tag via driver
        
        return Task.FromResult(NodeExecutionResult.Ok(new { tagId, tagPath, writtenValue = inputValue, success = true }));
    }
}

/// <summary>
/// Math add node executor.
/// </summary>
public class MathAddExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "math-add";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 0);
        return Task.FromResult(NodeExecutionResult.Ok(a + b));
    }
}

/// <summary>
/// Math subtract node executor.
/// </summary>
public class MathSubtractExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "math-subtract";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 0);
        return Task.FromResult(NodeExecutionResult.Ok(a - b));
    }
}

/// <summary>
/// Math multiply node executor.
/// </summary>
public class MathMultiplyExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "math-multiply";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 0);
        return Task.FromResult(NodeExecutionResult.Ok(a * b));
    }
}

/// <summary>
/// Math divide node executor.
/// </summary>
public class MathDivideExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "math-divide";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 1);
        
        if (Math.Abs(b) < double.Epsilon)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Division by zero"));
        }
        
        return Task.FromResult(NodeExecutionResult.Ok(a / b));
    }
}

/// <summary>
/// Compare equal node executor.
/// </summary>
public class CompareEqualExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "compare-equal";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = GetInput(node, context, "a");
        var b = GetInput(node, context, "b");
        return Task.FromResult(NodeExecutionResult.Ok(Equals(a, b)));
    }
}

/// <summary>
/// Compare greater than node executor.
/// </summary>
public class CompareGreaterExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "compare-greater";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 0);
        return Task.FromResult(NodeExecutionResult.Ok(a > b));
    }
}

/// <summary>
/// Compare less than node executor.
/// </summary>
public class CompareLessExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "compare-less";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var a = Convert.ToDouble(GetInput(node, context, "a") ?? 0);
        var b = Convert.ToDouble(GetInput(node, context, "b") ?? 0);
        return Task.FromResult(NodeExecutionResult.Ok(a < b));
    }
}

/// <summary>
/// If/conditional node executor.
/// </summary>
public class LogicIfExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "logic-if";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var condition = Convert.ToBoolean(GetInput(node, context, "condition") ?? false);
        var thenValue = GetInput(node, context, "then");
        var elseValue = GetInput(node, context, "else");
        
        return Task.FromResult(NodeExecutionResult.Ok(condition ? thenValue : elseValue));
    }
}

/// <summary>
/// Debug log node executor.
/// </summary>
public class DebugLogExecutor : NodeExecutorBase
{
    /// <inheritdoc />
    public override string NodeType => "debug-log";

    /// <inheritdoc />
    public override Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var value = GetInput(node, context, "value");
        var label = GetConfig<string>(node, "label") ?? "Debug";
        
        // Log the value - in production this would go to the flow execution log
        Console.WriteLine($"[{label}] {value}");
        
        return Task.FromResult(NodeExecutionResult.Ok(new { logged = true, label, value, timestamp = DateTime.UtcNow }));
    }
}
