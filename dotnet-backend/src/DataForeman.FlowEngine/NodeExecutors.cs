using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DataForeman.RedisStreams;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

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

/// <summary>
/// C# Script executor using Roslyn.
/// </summary>
public class CSharpScriptExecutor : NodeExecutorBase
{
    private static readonly ScriptOptions DefaultScriptOptions = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(System.Collections.Generic.List<>).Assembly,
            typeof(System.Math).Assembly,
            typeof(System.DateTime).Assembly
        )
        .WithImports(
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "System.Math"
        );

    /// <inheritdoc />
    public override string NodeType => "csharp";

    /// <inheritdoc />
    public override async Task<NodeExecutionResult> ExecuteAsync(FlowNode node, FlowExecutionContext context)
    {
        var code = GetConfig<string>(node, "code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return NodeExecutionResult.Fail("No C# code provided");
        }

        try
        {
            // Create globals object with access to input, tags, and flow context
            var globals = new ScriptGlobals
            {
                input = CreateInputObject(node, context),
                tags = context.Parameters ?? new Dictionary<string, object?>(),
                flow = new
                {
                    id = context.FlowId,
                    executionId = context.ExecutionId,
                    parameters = context.Parameters
                }
            };

            // Create and run the script with a timeout
            var script = CSharpScript.Create<object>(code, DefaultScriptOptions, typeof(ScriptGlobals));
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await script.RunAsync(globals, cts.Token);
            
            return NodeExecutionResult.Ok(result.ReturnValue);
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join("; ", ex.Diagnostics.Select(d => d.GetMessage()));
            return NodeExecutionResult.Fail($"C# compilation error: {errors}");
        }
        catch (OperationCanceledException)
        {
            return NodeExecutionResult.Fail("C# script execution timed out (5 seconds)");
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"C# script execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a dynamic input object from connected nodes.
    /// </summary>
    private dynamic CreateInputObject(FlowNode node, FlowExecutionContext context)
    {
        var inputDict = new Dictionary<string, object?>();
        
        // Collect all inputs from connected nodes
        if (node.Config != null)
        {
            foreach (var kvp in node.Config)
            {
                if (kvp.Key.StartsWith("input_"))
                {
                    var inputName = kvp.Key.Substring(6); // Remove "input_" prefix
                    var value = GetInput(node, context, inputName);
                    inputDict[inputName] = value;
                }
            }
        }
        
        // Convert to expandoobject for dynamic access
        return new System.Dynamic.ExpandoObject();
    }
}

/// <summary>
/// Global variables available in C# scripts.
/// </summary>
public class ScriptGlobals
{
    /// <summary>
    /// Input values from connected nodes.
    /// </summary>
    public dynamic input { get; set; } = new System.Dynamic.ExpandoObject();

    /// <summary>
    /// Access to tag values and parameters.
    /// </summary>
    public Dictionary<string, object?> tags { get; set; } = new();

    /// <summary>
    /// Flow execution context information.
    /// </summary>
    public dynamic flow { get; set; } = new { };
}
