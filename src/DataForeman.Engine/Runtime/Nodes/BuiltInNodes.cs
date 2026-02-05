// DataForeman Platform - AI Agent Implementation Directives
// Built-in Node Runtimes - Core node implementations
// Each node is a deterministic processing unit with declared ports.

using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;
using System.Text.Json;

namespace DataForeman.Engine.Runtime.Nodes;

/// <summary>
/// Base class for node runtimes with common utilities.
/// </summary>
public abstract class NodeRuntimeBase : INodeRuntime
{
    public abstract ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct);

    protected static JsonElement CreatePayload(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}

/// <summary>
/// Manual trigger node - starts flow execution manually.
/// </summary>
public sealed class ManualTriggerRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "trigger-manual",
        DisplayName = "Manual Trigger",
        Category = "Triggers",
        Description = "Manually triggers flow execution",
        Icon = "fa-play",
        Color = "#e74c3c",
        IsTrigger = true,
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        context.Logger.Debug("Manual trigger activated");
        context.Emitter.Emit("output", context.Message);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Timer/Inject trigger node - triggers on schedule.
/// </summary>
public sealed class TimerTriggerRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "trigger-schedule",
        DisplayName = "Timer",
        Category = "Triggers",
        Description = "Triggers flow execution on a schedule",
        Icon = "fa-clock",
        Color = "#e74c3c",
        IsTrigger = true,
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "intervalMs", Label = "Interval (ms)", Type = "number", Required = true },
                new ConfigProperty { Name = "payload", Label = "Payload", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<TimerConfig>();
        var payload = config?.Payload ?? new { timestamp = context.CurrentUtc };
        
        var msg = context.Message.Derive(context.CurrentUtc, CreatePayload(payload));
        context.Emitter.Emit("output", msg);
        return ValueTask.CompletedTask;
    }

    private sealed class TimerConfig
    {
        public int IntervalMs { get; set; } = 1000;
        public object? Payload { get; set; }
    }
}

/// <summary>
/// Tag Input node - reads a tag value.
/// </summary>
public sealed class TagInputRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "tag-input",
        DisplayName = "Tag Input",
        Category = "Tags",
        Description = "Reads a tag value",
        Icon = "fa-tag",
        Color = "#3498db",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Trigger", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Value", Direction = PortDirection.Output },
            new PortDescriptor { Name = "error", Label = "Error", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "tagPath", Label = "Tag Path", Type = "tagPath", Required = true }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<TagInputConfig>();
        if (string.IsNullOrEmpty(config?.TagPath))
        {
            context.Logger.Error("Tag path not configured");
            context.Emitter.EmitError(new InvalidOperationException("Tag path not configured"), context.Message);
            return;
        }

        try
        {
            var tagValue = await context.TagReader.GetValueAsync(config.TagPath, ct);
            if (tagValue == null)
            {
                context.Logger.Warn($"Tag '{config.TagPath}' not found or has no value");
                context.Emitter.EmitError(new InvalidOperationException($"Tag '{config.TagPath}' not found"), context.Message);
                return;
            }

            var msg = context.Message.Derive(context.CurrentUtc, CreatePayload(new
            {
                tagPath = tagValue.TagPath,
                value = tagValue.Value,
                timestamp = tagValue.TimestampUtc,
                quality = tagValue.Quality
            }));

            context.Logger.Debug($"Read tag '{config.TagPath}': {tagValue.Value}");
            context.Emitter.Emit("output", msg);
        }
        catch (Exception ex)
        {
            context.Logger.Error($"Failed to read tag '{config.TagPath}'", ex);
            context.Emitter.EmitError(ex, context.Message);
        }
    }

    private sealed class TagInputConfig
    {
        public string? TagPath { get; set; }
    }
}

/// <summary>
/// Tag Output node - writes a tag value.
/// </summary>
public sealed class TagOutputRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "tag-output",
        DisplayName = "Tag Output",
        Category = "Tags",
        Description = "Writes a value to a tag",
        Icon = "fa-tag",
        Color = "#2ecc71",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Done", Direction = PortDirection.Output },
            new PortDescriptor { Name = "error", Label = "Error", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "tagPath", Label = "Tag Path", Type = "tagPath", Required = true }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<TagOutputConfig>();
        if (string.IsNullOrEmpty(config?.TagPath))
        {
            context.Logger.Error("Tag path not configured");
            context.Emitter.EmitError(new InvalidOperationException("Tag path not configured"), context.Message);
            return;
        }

        try
        {
            var payload = context.Message.Payload;
            object? value = null;

            if (payload != null)
            {
                // Try to get value from payload
                if (payload.Value.TryGetProperty("value", out var valueProp))
                {
                    value = valueProp.ValueKind switch
                    {
                        JsonValueKind.Number => valueProp.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => valueProp.GetString(),
                        _ => valueProp.ToString()
                    };
                }
                else
                {
                    value = payload.Value.ToString();
                }
            }

            await context.TagWriter.WriteValueAsync(config.TagPath, value!, ct);
            context.Logger.Debug($"Wrote tag '{config.TagPath}': {value}");
            context.Emitter.Emit("output", context.Message);
        }
        catch (Exception ex)
        {
            context.Logger.Error($"Failed to write tag '{config.TagPath}'", ex);
            context.Emitter.EmitError(ex, context.Message);
        }
    }

    private sealed class TagOutputConfig
    {
        public string? TagPath { get; set; }
    }
}

/// <summary>
/// Debug node - outputs message to console/trace.
/// </summary>
public sealed class DebugRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "output-log",
        DisplayName = "Debug",
        Category = "Output",
        Description = "Outputs message to debug console",
        Icon = "fa-bug",
        Color = "#9b59b6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = Array.Empty<PortDescriptor>(),
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "logLevel", Label = "Log Level", Type = "select", 
                    Options = new[] {
                        new SelectOption { Value = "debug", Label = "Debug" },
                        new SelectOption { Value = "info", Label = "Info" },
                        new SelectOption { Value = "warn", Label = "Warning" }
                    }
                },
                new ConfigProperty { Name = "outputProperty", Label = "Output Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<DebugConfig>();
        var logLevel = config?.LogLevel ?? "info";
        var outputProperty = config?.OutputProperty;

        object? output;
        if (!string.IsNullOrEmpty(outputProperty) && context.Message.Payload != null)
        {
            if (context.Message.Payload.Value.TryGetProperty(outputProperty, out var prop))
                output = prop.ToString();
            else
                output = context.Message.Payload.Value.ToString();
        }
        else
        {
            output = context.Message.Payload?.ToString() ?? "(no payload)";
        }

        var message = $"[{context.Message.CorrelationId}] {output}";

        switch (logLevel)
        {
            case "debug":
                context.Logger.Debug(message);
                break;
            case "warn":
                context.Logger.Warn(message);
                break;
            default:
                context.Logger.Info(message);
                break;
        }

        return ValueTask.CompletedTask;
    }

    private sealed class DebugConfig
    {
        public string? LogLevel { get; set; }
        public string? OutputProperty { get; set; }
    }
}

/// <summary>
/// Compare node - compares values.
/// </summary>
public sealed class CompareRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "logic-compare",
        DisplayName = "Compare",
        Category = "Logic",
        Description = "Compares a value against a threshold",
        Icon = "fa-not-equal",
        Color = "#f39c12",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "true", Label = "True", Direction = PortDirection.Output },
            new PortDescriptor { Name = "false", Label = "False", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "operator", Label = "Operator", Type = "select", Required = true,
                    Options = new[] {
                        new SelectOption { Value = "eq", Label = "==" },
                        new SelectOption { Value = "neq", Label = "!=" },
                        new SelectOption { Value = "gt", Label = ">" },
                        new SelectOption { Value = "gte", Label = ">=" },
                        new SelectOption { Value = "lt", Label = "<" },
                        new SelectOption { Value = "lte", Label = "<=" }
                    }
                },
                new ConfigProperty { Name = "threshold", Label = "Threshold", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<CompareConfig>();
        if (config == null)
        {
            context.Emitter.Emit("false", context.Message);
            return ValueTask.CompletedTask;
        }

        double value = 0;
        if (context.Message.Payload != null)
        {
            var property = config.Property ?? "value";
            if (context.Message.Payload.Value.TryGetProperty(property, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    value = prop.GetDouble();
                else if (double.TryParse(prop.ToString(), out var parsed))
                    value = parsed;
            }
        }

        const double FloatEpsilon = 0.0001; // Tolerance for floating-point equality comparison
        var result = config.Operator switch
        {
            "eq" => Math.Abs(value - config.Threshold) < FloatEpsilon,
            "neq" => Math.Abs(value - config.Threshold) >= FloatEpsilon,
            "gt" => value > config.Threshold,
            "gte" => value >= config.Threshold,
            "lt" => value < config.Threshold,
            "lte" => value <= config.Threshold,
            _ => false
        };

        context.Logger.Debug($"Compare {value} {config.Operator} {config.Threshold} = {result}");
        context.Emitter.Emit(result ? "true" : "false", context.Message);
        return ValueTask.CompletedTask;
    }

    private sealed class CompareConfig
    {
        public string Operator { get; set; } = "gt";
        public double Threshold { get; set; }
        public string? Property { get; set; }
    }
}

/// <summary>
/// Math Add node.
/// </summary>
public sealed class MathAddRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "math-add",
        DisplayName = "Add",
        Category = "Math",
        Description = "Adds a constant to the input value",
        Icon = "fa-plus",
        Color = "#1abc9c",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Result", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "addend", Label = "Add Value", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<MathConfig>();
        var value = ExtractValue(context.Message, config?.Property);
        var result = value + (config?.Addend ?? 0);

        var msg = context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result }));
        context.Emitter.Emit("output", msg);
        return ValueTask.CompletedTask;
    }

    private static double ExtractValue(MessageEnvelope message, string? property)
    {
        if (message.Payload == null) return 0;
        var prop = property ?? "value";
        if (message.Payload.Value.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetDouble();
        return 0;
    }

    private sealed class MathConfig
    {
        public double Addend { get; set; }
        public string? Property { get; set; }
    }
}

/// <summary>
/// Math Multiply node.
/// </summary>
public sealed class MathMultiplyRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "math-multiply",
        DisplayName = "Multiply",
        Category = "Math",
        Description = "Multiplies the input value by a constant",
        Icon = "fa-times",
        Color = "#1abc9c",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Result", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "multiplier", Label = "Multiply By", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<MathConfig>();
        var value = ExtractValue(context.Message, config?.Property);
        var result = value * (config?.Multiplier ?? 1);

        var msg = context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result }));
        context.Emitter.Emit("output", msg);
        return ValueTask.CompletedTask;
    }

    private static double ExtractValue(MessageEnvelope message, string? property)
    {
        if (message.Payload == null) return 0;
        var prop = property ?? "value";
        if (message.Payload.Value.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetDouble();
        return 0;
    }

    private sealed class MathConfig
    {
        public double Multiplier { get; set; } = 1;
        public string? Property { get; set; }
    }
}

/// <summary>
/// Scale node - linear scaling with min/max.
/// </summary>
public sealed class ScaleRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "math-scale",
        DisplayName = "Scale",
        Category = "Math",
        Description = "Linear scaling from input range to output range",
        Icon = "fa-arrows-alt-h",
        Color = "#1abc9c",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Result", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "inMin", Label = "Input Min", Type = "number", Required = true },
                new ConfigProperty { Name = "inMax", Label = "Input Max", Type = "number", Required = true },
                new ConfigProperty { Name = "outMin", Label = "Output Min", Type = "number", Required = true },
                new ConfigProperty { Name = "outMax", Label = "Output Max", Type = "number", Required = true },
                new ConfigProperty { Name = "clamp", Label = "Clamp Output", Type = "boolean" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<ScaleConfig>() ?? new ScaleConfig();
        var value = ExtractValue(context.Message, config.Property);

        // Linear interpolation
        var inRange = config.InMax - config.InMin;
        var outRange = config.OutMax - config.OutMin;
        var result = config.OutMin + ((value - config.InMin) / inRange) * outRange;

        // Clamp if requested
        if (config.Clamp)
        {
            result = Math.Max(config.OutMin, Math.Min(config.OutMax, result));
        }

        var msg = context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result }));
        context.Emitter.Emit("output", msg);
        return ValueTask.CompletedTask;
    }

    private static double ExtractValue(MessageEnvelope message, string? property)
    {
        if (message.Payload == null) return 0;
        var prop = property ?? "value";
        if (message.Payload.Value.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetDouble();
        return 0;
    }

    private sealed class ScaleConfig
    {
        public double InMin { get; set; }
        public double InMax { get; set; } = 100;
        public double OutMin { get; set; }
        public double OutMax { get; set; } = 100;
        public bool Clamp { get; set; }
        public string? Property { get; set; }
    }
}

/// <summary>
/// Subflow Input node - internal node for subflow input port.
/// </summary>
public sealed class SubflowInputRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "subflow-input",
        DisplayName = "Subflow Input",
        Category = "Internal",
        Description = "Input port for subflow",
        Icon = "fa-sign-in-alt",
        Color = "#9b59b6",
        IsFlowNode = false, // Cannot be used in normal flows
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "portName", Label = "Port Name", Type = "string", Required = true }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Simply pass through the message
        context.Emitter.Emit("output", context.Message);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Subflow Output node - internal node for subflow output port.
/// </summary>
public sealed class SubflowOutputRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "subflow-output",
        DisplayName = "Subflow Output",
        Category = "Internal",
        Description = "Output port for subflow",
        Icon = "fa-sign-out-alt",
        Color = "#9b59b6",
        IsFlowNode = false, // Cannot be used in normal flows
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = Array.Empty<PortDescriptor>(),
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "portName", Label = "Port Name", Type = "string", Required = true }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Output is handled by subflow executor
        return ValueTask.CompletedTask;
    }
}
