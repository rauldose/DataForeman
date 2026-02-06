// DataForeman Platform - Extended Node Runtimes
// Implements all node types defined in the App's NodePluginRegistry
// that were previously missing from the Engine.

using DataForeman.Engine.Services;
using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataForeman.Engine.Runtime.Nodes;

// ─── Output ─────────────────────────────────────────────────────

/// <summary>
/// Notification output — logs a formatted message with severity level.
/// </summary>
public sealed class NotificationRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "output-notification",
        DisplayName = "Notification",
        Category = "Output",
        Description = "Sends a notification with configurable severity",
        Icon = "fa-bell",
        Color = "#e74c3c",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = Array.Empty<PortDescriptor>(),
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "message", Label = "Message Template", Type = "string" },
                new ConfigProperty { Name = "severity", Label = "Severity", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "Info", Label = "Info" },
                        new SelectOption { Value = "Warning", Label = "Warning" },
                        new SelectOption { Value = "Critical", Label = "Critical" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<NotifyCfg>();
        var template = cfg?.Message ?? "Notification: {{value}}";
        var severity = cfg?.Severity ?? "Info";

        var rendered = RenderTemplate(template, context.Message.Payload);

        switch (severity)
        {
            case "Critical":
                context.Logger.Error($"[CRITICAL] {rendered}");
                break;
            case "Warning":
                context.Logger.Warn($"[WARNING] {rendered}");
                break;
            default:
                context.Logger.Info($"[INFO] {rendered}");
                break;
        }

        return ValueTask.CompletedTask;
    }

    private static string RenderTemplate(string template, JsonElement? payload)
    {
        if (payload == null) return template;
        return Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (payload.Value.TryGetProperty(key, out var prop))
                return prop.ToString();
            return match.Value;
        });
    }

    private sealed class NotifyCfg
    {
        public string? Message { get; set; }
        public string? Severity { get; set; }
    }
}

// ─── Math ───────────────────────────────────────────────────────

/// <summary>
/// Subtracts a constant from the input value.
/// </summary>
public sealed class MathSubtractRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "math-subtract",
        DisplayName = "Subtract",
        Category = "Math",
        Description = "Subtracts a constant from the input value",
        Icon = "fa-minus",
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
                new ConfigProperty { Name = "operand", Label = "Subtract Value", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<OpCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var result = val - (cfg?.Operand ?? 0);
        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private sealed class OpCfg { public double Operand { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// Divides the input value by a constant.
/// </summary>
public sealed class MathDivideRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "math-divide",
        DisplayName = "Divide",
        Category = "Math",
        Description = "Divides the input value by a constant",
        Icon = "fa-divide",
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
                new ConfigProperty { Name = "operand", Label = "Divide By", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<OpCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var divisor = cfg?.Operand ?? 1;
        if (Math.Abs(divisor) < 1e-15)
        {
            context.Logger.Warn("Division by zero — emitting 0");
            divisor = 1;
        }
        var result = val / divisor;
        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private sealed class OpCfg { public double Operand { get; set; } = 1; public string? Property { get; set; } }
}

// ─── Logic ──────────────────────────────────────────────────────

/// <summary>
/// Branch node — routes to true/false output based on condition.
/// </summary>
public sealed class BranchRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "logic-branch",
        DisplayName = "Branch",
        Category = "Logic",
        Description = "Routes messages based on a condition",
        Icon = "fa-code-branch",
        Color = "#f39c12",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
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
                new ConfigProperty { Name = "condition", Label = "Condition", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "truthy", Label = "If Truthy" },
                        new SelectOption { Value = "equals", Label = "Equals Value" },
                        new SelectOption { Value = "greater", Label = "Greater Than" },
                        new SelectOption { Value = "less", Label = "Less Than" }
                    }
                },
                new ConfigProperty { Name = "threshold", Label = "Compare Value", Type = "number" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<BranchCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var threshold = cfg?.Threshold ?? 0;

        var pass = (cfg?.Condition ?? "truthy") switch
        {
            "equals" => Math.Abs(val - threshold) < NumericHelper.FloatEpsilon,
            "greater" => val > threshold,
            "less" => val < threshold,
            _ => Math.Abs(val) >= NumericHelper.FloatEpsilon // truthy = non-zero
        };

        context.Emitter.Emit(pass ? "true" : "false", context.Message);
        return ValueTask.CompletedTask;
    }

    private sealed class BranchCfg { public string? Condition { get; set; } public double Threshold { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// AND gate — emits only when both inputs have been received.
/// </summary>
public sealed class LogicAndRuntime : NodeRuntimeBase
{
    private JsonElement? _lastA;
    private JsonElement? _lastB;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "logic-and",
        DisplayName = "AND",
        Category = "Logic",
        Description = "Outputs when both inputs have truthy values",
        Icon = "fa-circle-nodes",
        Color = "#f39c12",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "inputA", Label = "Input A", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "inputB", Label = "Input B", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "true", Label = "True", Direction = PortDirection.Output },
            new PortDescriptor { Name = "false", Label = "False", Direction = PortDirection.Output }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Track which input port the message arrived on
        var portName = context.Message.SourcePort ?? "inputA";
        if (portName == "inputB")
            _lastB = context.Message.Payload;
        else
            _lastA = context.Message.Payload;

        var aTruthy = NumericHelper.IsTruthy(_lastA);
        var bTruthy = NumericHelper.IsTruthy(_lastB);

        context.Emitter.Emit(aTruthy && bTruthy ? "true" : "false", context.Message);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// OR gate — emits true when either input is truthy.
/// </summary>
public sealed class LogicOrRuntime : NodeRuntimeBase
{
    private JsonElement? _lastA;
    private JsonElement? _lastB;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "logic-or",
        DisplayName = "OR",
        Category = "Logic",
        Description = "Outputs true when either input is truthy",
        Icon = "fa-circle-nodes",
        Color = "#f39c12",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "inputA", Label = "Input A", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "inputB", Label = "Input B", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "true", Label = "True", Direction = PortDirection.Output },
            new PortDescriptor { Name = "false", Label = "False", Direction = PortDirection.Output }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var portName = context.Message.SourcePort ?? "inputA";
        if (portName == "inputB")
            _lastB = context.Message.Payload;
        else
            _lastA = context.Message.Payload;

        var result = NumericHelper.IsTruthy(_lastA) || NumericHelper.IsTruthy(_lastB);
        context.Emitter.Emit(result ? "true" : "false", context.Message);
        return ValueTask.CompletedTask;
    }
}

// ─── Utility ────────────────────────────────────────────────────

/// <summary>
/// Delay node — holds a message for a configured duration before forwarding.
/// </summary>
public sealed class DelayRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "util-delay",
        DisplayName = "Delay",
        Category = "Utility",
        Description = "Delays message forwarding by a configurable duration",
        Icon = "fa-clock",
        Color = "#95a5a6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "delay", Label = "Delay (ms)", Type = "number", Required = true }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<DelayCfg>();
        var delayMs = Math.Clamp(cfg?.Delay ?? 1000, 0, 60000);

        if (delayMs > 0)
            await Task.Delay(delayMs, ct);

        context.Emitter.Emit("output", context.Message);
    }

    private sealed class DelayCfg { public int Delay { get; set; } = 1000; }
}

/// <summary>
/// Filter node — passes messages only when condition is met (value changed, non-zero, or non-null).
/// </summary>
public sealed class FilterRuntime : NodeRuntimeBase
{
    private string? _previousValueStr;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "util-filter",
        DisplayName = "Filter",
        Category = "Utility",
        Description = "Passes messages only when a condition is met",
        Icon = "fa-filter",
        Color = "#95a5a6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Passed", Direction = PortDirection.Output },
            new PortDescriptor { Name = "rejected", Label = "Rejected", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "condition", Label = "Pass If", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "changed", Label = "Value Changed" },
                        new SelectOption { Value = "nonzero", Label = "Non-Zero" },
                        new SelectOption { Value = "valid", Label = "Valid (not null)" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<FilterCfg>();
        var prop = cfg?.Property ?? "value";
        var condition = cfg?.Condition ?? "changed";

        string? currentStr = null;
        double currentNum = 0;
        bool hasValue = false;

        if (context.Message.Payload != null && context.Message.Payload.Value.TryGetProperty(prop, out var val))
        {
            currentStr = val.ToString();
            hasValue = val.ValueKind != JsonValueKind.Null && val.ValueKind != JsonValueKind.Undefined;
            if (val.ValueKind == JsonValueKind.Number)
                currentNum = val.GetDouble();
        }

        bool pass = condition switch
        {
            "nonzero" => Math.Abs(currentNum) >= NumericHelper.FloatEpsilon,
            "valid" => hasValue,
            _ => currentStr != _previousValueStr // "changed"
        };

        _previousValueStr = currentStr;

        context.Emitter.Emit(pass ? "output" : "rejected", context.Message);
        return ValueTask.CompletedTask;
    }

    private sealed class FilterCfg { public string? Condition { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// Constant node — emits a fixed value.
/// </summary>
public sealed class ConstantRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "util-constant",
        DisplayName = "Constant",
        Category = "Utility",
        Description = "Emits a constant value",
        Icon = "fa-hashtag",
        Color = "#95a5a6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Trigger", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Value", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "value", Label = "Value", Type = "string", Required = true },
                new ConfigProperty { Name = "valueType", Label = "Type", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "number", Label = "Number" },
                        new SelectOption { Value = "string", Label = "String" },
                        new SelectOption { Value = "boolean", Label = "Boolean" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<ConstCfg>();
        var raw = cfg?.Value ?? "0";
        var vtype = cfg?.ValueType ?? "number";

        object boxed = vtype switch
        {
            "boolean" => raw.Equals("true", StringComparison.OrdinalIgnoreCase),
            "string" => raw,
            _ => double.TryParse(raw, out var n) ? n : 0.0
        };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = boxed })));
        return ValueTask.CompletedTask;
    }

    private sealed class ConstCfg { public string? Value { get; set; } public string? ValueType { get; set; } }
}

// ─── Data Processing ────────────────────────────────────────────

/// <summary>
/// Smooth node — applies EMA, SMA, or Median filter to incoming values.
/// </summary>
public sealed class SmoothRuntime : NodeRuntimeBase
{
    private readonly List<double> _windowBuffer = new();
    private double _emaValue = double.NaN;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "data-smooth",
        DisplayName = "Smooth",
        Category = "Data",
        Description = "Applies smoothing algorithm to reduce noise",
        Icon = "fa-wave-square",
        Color = "#2ecc71",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Smoothed", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "algorithm", Label = "Algorithm", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "ema", Label = "Exponential Moving Avg" },
                        new SelectOption { Value = "sma", Label = "Simple Moving Avg" },
                        new SelectOption { Value = "median", Label = "Median Filter" }
                    }
                },
                new ConfigProperty { Name = "factor", Label = "Smoothing Factor / Window", Type = "number" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<SmoothCfg>();
        var raw = NumericHelper.Extract(context.Message, cfg?.Property);
        var factor = cfg?.Factor ?? 0.2;
        var algo = cfg?.Algorithm ?? "ema";

        double smoothed;

        switch (algo)
        {
            case "sma":
            {
                var windowSize = Math.Max(2, (int)factor);
                _windowBuffer.Add(raw);
                if (_windowBuffer.Count > windowSize)
                    _windowBuffer.RemoveAt(0);
                smoothed = _windowBuffer.Average();
                break;
            }
            case "median":
            {
                var windowSize = Math.Max(3, (int)factor);
                _windowBuffer.Add(raw);
                if (_windowBuffer.Count > windowSize)
                    _windowBuffer.RemoveAt(0);
                var sorted = _windowBuffer.OrderBy(v => v).ToList();
                smoothed = sorted[sorted.Count / 2];
                break;
            }
            default: // ema
            {
                var alpha = Math.Clamp(factor, 0.01, 1.0);
                if (double.IsNaN(_emaValue))
                    _emaValue = raw;
                else
                    _emaValue = alpha * raw + (1.0 - alpha) * _emaValue;
                smoothed = _emaValue;
                break;
            }
        }

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = smoothed })));
        return ValueTask.CompletedTask;
    }

    private sealed class SmoothCfg { public string? Algorithm { get; set; } public double Factor { get; set; } = 0.2; public string? Property { get; set; } }
}

/// <summary>
/// Aggregate node — computes running aggregation (avg, sum, min, max, count) over a window.
/// </summary>
public sealed class AggregateRuntime : NodeRuntimeBase
{
    private readonly List<double> _buffer = new();

    public static NodeDescriptor Descriptor => new()
    {
        Type = "data-aggregate",
        DisplayName = "Aggregate",
        Category = "Data",
        Description = "Aggregates values over a configurable window",
        Icon = "fa-chart-bar",
        Color = "#2ecc71",
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
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "avg", Label = "Average" },
                        new SelectOption { Value = "sum", Label = "Sum" },
                        new SelectOption { Value = "min", Label = "Minimum" },
                        new SelectOption { Value = "max", Label = "Maximum" },
                        new SelectOption { Value = "count", Label = "Count" }
                    }
                },
                new ConfigProperty { Name = "windowSize", Label = "Window Size", Type = "number", Required = true },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<AggCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var windowSize = Math.Max(1, cfg?.WindowSize ?? 10);

        _buffer.Add(val);
        if (_buffer.Count > windowSize)
            _buffer.RemoveAt(0);

        var result = (cfg?.Operation ?? "avg") switch
        {
            "sum" => _buffer.Sum(),
            "min" => _buffer.Min(),
            "max" => _buffer.Max(),
            "count" => _buffer.Count,
            _ => _buffer.Average()
        };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result, count = _buffer.Count })));
        return ValueTask.CompletedTask;
    }

    private sealed class AggCfg { public string? Operation { get; set; } public int WindowSize { get; set; } = 10; public string? Property { get; set; } }
}

/// <summary>
/// Deadband node — only passes value when it changes by more than the configured threshold.
/// </summary>
public sealed class DeadbandRuntime : NodeRuntimeBase
{
    private double _lastEmittedValue = double.NaN;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "data-deadband",
        DisplayName = "Deadband",
        Category = "Data",
        Description = "Suppresses values within a deadband threshold",
        Icon = "fa-arrows-left-right",
        Color = "#2ecc71",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Changed", Direction = PortDirection.Output },
            new PortDescriptor { Name = "suppressed", Label = "Suppressed", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "threshold", Label = "Threshold", Type = "number", Required = true },
                new ConfigProperty { Name = "type", Label = "Type", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "absolute", Label = "Absolute" },
                        new SelectOption { Value = "percentage", Label = "Percentage" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<DeadbandCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var threshold = cfg?.Threshold ?? 0.5;
        var isPercentage = (cfg?.Type ?? "absolute") == "percentage";

        bool exceedsDeadband;
        if (double.IsNaN(_lastEmittedValue))
        {
            exceedsDeadband = true;
        }
        else
        {
            var diff = Math.Abs(val - _lastEmittedValue);
            if (isPercentage && Math.Abs(_lastEmittedValue) > 1e-15)
                diff = diff / Math.Abs(_lastEmittedValue) * 100.0;
            exceedsDeadband = diff >= threshold;
        }

        if (exceedsDeadband)
        {
            _lastEmittedValue = val;
            context.Emitter.Emit("output", context.Message);
        }
        else
        {
            context.Emitter.Emit("suppressed", context.Message);
        }

        return ValueTask.CompletedTask;
    }

    private sealed class DeadbandCfg { public double Threshold { get; set; } = 0.5; public string? Type { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// Rate of Change node — computes the rate of change (derivative) per time unit.
/// </summary>
public sealed class RateOfChangeRuntime : NodeRuntimeBase
{
    private double _prevValue = double.NaN;
    private DateTime _prevTimestamp;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "data-rateofchange",
        DisplayName = "Rate of Change",
        Category = "Data",
        Description = "Calculates the rate of change of a value over time",
        Icon = "fa-chart-line",
        Color = "#2ecc71",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Value", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Rate", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "unit", Label = "Time Unit", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "second", Label = "Per Second" },
                        new SelectOption { Value = "minute", Label = "Per Minute" },
                        new SelectOption { Value = "hour", Label = "Per Hour" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<RocCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var now = context.CurrentUtc;

        if (double.IsNaN(_prevValue))
        {
            _prevValue = val;
            _prevTimestamp = now;
            context.Emitter.Emit("output", context.Message.Derive(now, CreatePayload(new { value = 0.0, rate = 0.0 })));
            return ValueTask.CompletedTask;
        }

        var elapsed = (now - _prevTimestamp).TotalSeconds;
        if (elapsed < 0.001)
        {
            context.Emitter.Emit("output", context.Message.Derive(now, CreatePayload(new { value = val, rate = 0.0 })));
            return ValueTask.CompletedTask;
        }

        var ratePerSecond = (val - _prevValue) / elapsed;
        var multiplier = (cfg?.Unit ?? "second") switch
        {
            "minute" => 60.0,
            "hour" => 3600.0,
            _ => 1.0
        };

        _prevValue = val;
        _prevTimestamp = now;

        context.Emitter.Emit("output", context.Message.Derive(now, CreatePayload(new { value = val, rate = ratePerSecond * multiplier })));
        return ValueTask.CompletedTask;
    }

    private sealed class RocCfg { public string? Unit { get; set; } public string? Property { get; set; } }
}

// ─── Function ───────────────────────────────────────────────────

/// <summary>
/// Switch node — routes messages to different outputs based on property value matching rules.
/// </summary>
public sealed class SwitchRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "func-switch",
        DisplayName = "Switch",
        Category = "Function",
        Description = "Routes messages based on property value rules",
        Icon = "fa-arrows-split-up-and-left",
        Color = "#3498db",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output1", Label = "Output 1", Direction = PortDirection.Output },
            new PortDescriptor { Name = "output2", Label = "Output 2", Direction = PortDirection.Output },
            new PortDescriptor { Name = "default", Label = "Default", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "property", Label = "Property", Type = "string" },
                new ConfigProperty { Name = "rules", Label = "Rules (JSON)", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<SwitchCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);

        var rules = ParseRules(cfg?.Rules);
        bool matched = false;

        for (int i = 0; i < rules.Count && i < 2; i++)
        {
            var rule = rules[i];
            bool ruleMatch = rule.Op switch
            {
                ">" => val > rule.Val,
                ">=" => val >= rule.Val,
                "<" => val < rule.Val,
                "<=" => val <= rule.Val,
                "==" => Math.Abs(val - rule.Val) < NumericHelper.FloatEpsilon,
                "!=" => Math.Abs(val - rule.Val) >= NumericHelper.FloatEpsilon,
                _ => false
            };

            if (ruleMatch)
            {
                context.Emitter.Emit($"output{i + 1}", context.Message);
                matched = true;
            }
        }

        if (!matched)
            context.Emitter.Emit("default", context.Message);

        return ValueTask.CompletedTask;
    }

    private static List<SwitchRule> ParseRules(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<SwitchRule>>(rulesJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private sealed class SwitchCfg { public string? Property { get; set; } public string? Rules { get; set; } }
    private sealed class SwitchRule
    {
        [System.Text.Json.Serialization.JsonPropertyName("op")]
        public string Op { get; set; } = ">";
        [System.Text.Json.Serialization.JsonPropertyName("val")]
        public double Val { get; set; }
    }
}

/// <summary>
/// Template node — renders a string template with property substitution.
/// </summary>
public sealed class TemplateRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "func-template",
        DisplayName = "Template",
        Category = "Function",
        Description = "Renders a template with message property substitution",
        Icon = "fa-file-code",
        Color = "#3498db",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "template", Label = "Template", Type = "string" },
                new ConfigProperty { Name = "outputFormat", Label = "Output Format", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "text", Label = "Plain Text" },
                        new SelectOption { Value = "json", Label = "JSON" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<TplCfg>();
        var template = cfg?.Template ?? "{{payload}}";

        var rendered = Regex.Replace(template, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (key == "payload")
                return context.Message.Payload?.ToString() ?? "";
            if (context.Message.Payload != null && context.Message.Payload.Value.TryGetProperty(key, out var prop))
                return prop.ToString();
            return match.Value;
        });

        object payload = (cfg?.OutputFormat ?? "text") == "json"
            ? new { text = rendered }
            : (object)new { value = rendered };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(payload)));
        return ValueTask.CompletedTask;
    }

    private sealed class TplCfg { public string? Template { get; set; } public string? OutputFormat { get; set; } }
}

// ─── Trigger ────────────────────────────────────────────────────

/// <summary>
/// Tag trigger — fires when a monitored tag value changes.
/// </summary>
public sealed class TagTriggerRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "trigger-tag",
        DisplayName = "Tag Change",
        Category = "Triggers",
        Description = "Triggers when a tag value changes",
        Icon = "fa-bolt",
        Color = "#e74c3c",
        IsTrigger = true,
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Value", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "tagPath", Label = "Tag Path", Type = "tagPath", Required = true },
                new ConfigProperty { Name = "triggerOn", Label = "Trigger On", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "change", Label = "Any Change" },
                        new SelectOption { Value = "rising", Label = "Rising Edge" },
                        new SelectOption { Value = "falling", Label = "Falling Edge" }
                    }
                }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<TagTrigCfg>();
        if (string.IsNullOrEmpty(cfg?.TagPath))
        {
            context.Logger.Warn("Tag trigger: no tag path configured");
            return;
        }

        try
        {
            var tagVal = await context.TagReader.GetValueAsync(cfg.TagPath, ct);
            if (tagVal != null)
            {
                context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new
                {
                    tagPath = tagVal.TagPath,
                    value = tagVal.Value,
                    timestamp = tagVal.TimestampUtc
                })));
            }
        }
        catch (Exception ex)
        {
            context.Logger.Error($"Tag trigger failed: {ex.Message}");
        }
    }

    private sealed class TagTrigCfg { public string? TagPath { get; set; } public string? TriggerOn { get; set; } }
}

// ─── Communication ──────────────────────────────────────────────

/// <summary>
/// HTTP Request node — makes HTTP requests (GET/POST/PUT/DELETE).
/// </summary>
public sealed class HttpRequestRuntime : NodeRuntimeBase
{
    private static readonly HttpClient SharedClient = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public static NodeDescriptor Descriptor => new()
    {
        Type = "http-request",
        DisplayName = "HTTP Request",
        Category = "Communication",
        Description = "Makes HTTP requests to external APIs",
        Icon = "fa-globe",
        Color = "#9333ea",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Trigger", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Response", Direction = PortDirection.Output },
            new PortDescriptor { Name = "error", Label = "Error", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "method", Label = "Method", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "GET", Label = "GET" },
                        new SelectOption { Value = "POST", Label = "POST" },
                        new SelectOption { Value = "PUT", Label = "PUT" },
                        new SelectOption { Value = "DELETE", Label = "DELETE" }
                    }
                },
                new ConfigProperty { Name = "url", Label = "URL", Type = "string", Required = true },
                new ConfigProperty { Name = "timeout", Label = "Timeout (ms)", Type = "number" }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<HttpCfg>();
        if (string.IsNullOrEmpty(cfg?.Url))
        {
            context.Logger.Error("HTTP Request: URL not configured");
            context.Emitter.EmitError(new InvalidOperationException("URL not configured"), context.Message);
            return;
        }

        try
        {
            var method = (cfg.Method ?? "GET").ToUpperInvariant() switch
            {
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                _ => HttpMethod.Get
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            const int MinTimeoutMs = 1000;
            const int MaxTimeoutMs = 60000;
            cts.CancelAfter(Math.Clamp(cfg.Timeout, MinTimeoutMs, MaxTimeoutMs));

            using var request = new HttpRequestMessage(method, cfg.Url);

            if (method != HttpMethod.Get && context.Message.Payload != null)
                request.Content = new StringContent(context.Message.Payload.Value.ToString(), System.Text.Encoding.UTF8, "application/json");

            using var response = await SharedClient.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new
            {
                statusCode = (int)response.StatusCode,
                body,
                headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            })));
        }
        catch (Exception ex)
        {
            context.Logger.Error($"HTTP Request failed: {ex.Message}");
            context.Emitter.EmitError(ex, context.Message);
        }
    }

    private sealed class HttpCfg { public string? Method { get; set; } public string? Url { get; set; } public int Timeout { get; set; } = 5000; }
}

// ─── Script ─────────────────────────────────────────────────────

/// <summary>
/// C# Script node — executes C# code using CSharpScriptService.
/// Since CSharpScriptService is not injected, this node evaluates the
/// code as a simple expression against the message payload.
/// </summary>
public sealed class CSharpScriptRuntime : NodeRuntimeBase
{
    private readonly CSharpScriptService? _scriptService;
    private readonly Dictionary<string, object?> _nodeState = new();

    public CSharpScriptRuntime(CSharpScriptService? scriptService = null)
    {
        _scriptService = scriptService;
    }

    public static NodeDescriptor Descriptor => new()
    {
        Type = "script-csharp",
        DisplayName = "C# Script",
        Category = "Scripts",
        Description = "Executes C# code for custom processing",
        Icon = "fa-code",
        Color = "#178600",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Result", Direction = PortDirection.Output },
            new PortDescriptor { Name = "error", Label = "Error", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "code", Label = "C# Code", Type = "string" },
                new ConfigProperty { Name = "timeout", Label = "Timeout (ms)", Type = "number" },
                new ConfigProperty { Name = "onError", Label = "On Error", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "stop", Label = "Stop flow" },
                        new SelectOption { Value = "continue", Label = "Continue with null" }
                    }
                }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<ScriptCfg>();
        if (string.IsNullOrWhiteSpace(cfg?.Code))
        {
            context.Logger.Warn("C# Script: no code configured");
            context.Emitter.Emit("output", context.Message);
            return;
        }

        if (_scriptService == null)
        {
            context.Logger.Warn("C# Script: CSharpScriptService not available — passing through");
            context.Emitter.Emit("output", context.Message);
            return;
        }

        try
        {
            // Extract input value from message payload
            object? inputValue = null;
            if (context.Message.Payload.HasValue)
            {
                var p = context.Message.Payload.Value;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d))
                    inputValue = d;
                else if (p.ValueKind == JsonValueKind.String)
                    inputValue = p.GetString();
                else if (p.TryGetProperty("value", out var vp) && vp.ValueKind == JsonValueKind.Number)
                    inputValue = vp.GetDouble();
                else
                    inputValue = p.ToString();
            }

            var timeout = cfg.Timeout > 0 ? cfg.Timeout : 5000;
            var result = await _scriptService.ExecuteAsync(cfg.Code, _nodeState, inputValue, timeout, ct);

            // Log script output
            foreach (var logMsg in result.LogMessages)
                context.Logger.Info($"[script] {logMsg}");

            if (result.Success)
            {
                if (result.ReturnValue != null)
                {
                    var outPayload = CreatePayload(result.ReturnValue);
                    var outMessage = context.Message with { Payload = outPayload };
                    context.Emitter.Emit("output", outMessage);
                }
                else
                {
                    // null return = suppress output (deadband-style)
                    context.Logger.Info("Script returned null — output suppressed");
                }
            }
            else
            {
                var ex = new InvalidOperationException(result.ErrorMessage ?? "Script execution failed");
                if ((cfg.OnError ?? "stop") == "continue")
                {
                    context.Logger.Warn($"Script error (continuing): {result.ErrorMessage}");
                    context.Emitter.Emit("output", context.Message);
                }
                else
                {
                    context.Logger.Error($"Script error: {result.ErrorMessage}");
                    context.Emitter.EmitError(ex, context.Message);
                }
            }
        }
        catch (Exception ex)
        {
            if ((cfg.OnError ?? "stop") == "continue")
            {
                context.Logger.Warn($"Script error (continuing): {ex.Message}");
                context.Emitter.Emit("output", context.Message);
            }
            else
            {
                context.Logger.Error($"Script error: {ex.Message}");
                context.Emitter.EmitError(ex, context.Message);
            }
        }
    }

    private sealed class ScriptCfg { public string? Code { get; set; } public int Timeout { get; set; } = 5000; public string? OnError { get; set; } }
}

// ─── Script and function nodes ──────────────────────────────────

/// <summary>
/// JavaScript function node — executes user JS code via Jint interpreter.
/// </summary>
public sealed class JavaScriptRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "func-javascript",
        DisplayName = "JavaScript",
        Category = "Function",
        Description = "Runs JavaScript code via Jint interpreter",
        Icon = "fa-js",
        Color = "#f7df1e",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "code", Label = "Function Code", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<JsCfg>();
        if (string.IsNullOrWhiteSpace(cfg?.Code))
        {
            context.Logger.Warn("JavaScript: no code configured");
            context.Emitter.Emit("output", context.Message);
            return ValueTask.CompletedTask;
        }

        try
        {
            // Extract input value
            object? inputValue = null;
            if (context.Message.Payload.HasValue)
            {
                var p = context.Message.Payload.Value;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d))
                    inputValue = d;
                else if (p.ValueKind == JsonValueKind.String)
                    inputValue = p.GetString();
                else if (p.TryGetProperty("value", out var vp) && vp.ValueKind == JsonValueKind.Number)
                    inputValue = vp.GetDouble();
                else
                    inputValue = p.ToString();
            }

            var engine = new Jint.Engine(opts =>
            {
                Jint.ConstraintsOptionsExtensions.TimeoutInterval(opts, TimeSpan.FromSeconds(5));
                Jint.ConstraintsOptionsExtensions.MaxStatements(opts, 10000);
                Jint.OptionsExtensions.LimitRecursion(opts, 64);
            });

            // Expose input value and logging to script
            engine.SetValue("input", inputValue);
            engine.SetValue("log", new Action<object>(msg => context.Logger.Info($"[js] {msg}")));

            var result = engine.Evaluate(cfg.Code);
            var nativeResult = result.ToObject();

            if (nativeResult != null)
            {
                var payload = CreatePayload(nativeResult);
                context.Emitter.Emit("output", context.Message with { Payload = payload });
            }
            else
            {
                context.Emitter.Emit("output", context.Message);
            }
        }
        catch (Exception ex)
        {
            context.Logger.Error($"JavaScript error: {ex.Message}");
            context.Emitter.EmitError(ex, context.Message);
        }

        return ValueTask.CompletedTask;
    }

    private sealed class JsCfg { public string? Code { get; set; } }
}

/// <summary>
/// Inject timer — periodically sends a message (similar to trigger-schedule but inline).
/// </summary>
public sealed class InjectTimerRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "inject-timer",
        DisplayName = "Inject",
        Category = "Triggers",
        Description = "Periodically injects a message",
        Icon = "fa-syringe",
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
        context.Emitter.Emit("output", context.Message);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Debug sidebar — outputs to the debug sidebar (logs info).
/// </summary>
public sealed class DebugSidebarRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "debug-sidebar",
        DisplayName = "Debug Sidebar",
        Category = "Output",
        Description = "Outputs to the debug sidebar",
        Icon = "fa-bug",
        Color = "#9b59b6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = Array.Empty<PortDescriptor>()
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        context.Logger.Info($"[Debug] {context.Message.Payload?.ToString() ?? "(empty)"}");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Link In — receives from Link Out nodes (passthrough).
/// </summary>
public sealed class LinkInRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "link-in",
        DisplayName = "Link In",
        Category = "Utility",
        Description = "Receives messages from Link Out nodes",
        Icon = "fa-link",
        Color = "#95a5a6",
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "linkName", Label = "Link Name", Type = "string", Required = true }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        context.Emitter.Emit("output", context.Message);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Link Out — sends to Link In nodes (passthrough).
/// </summary>
public sealed class LinkOutRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "link-out",
        DisplayName = "Link Out",
        Category = "Utility",
        Description = "Sends messages to Link In nodes",
        Icon = "fa-link",
        Color = "#95a5a6",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = Array.Empty<PortDescriptor>(),
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "linkName", Label = "Link Name", Type = "string", Required = true }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        // Link routing is handled at the flow executor level
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Storage File — reads/writes data to a file (placeholder).
/// </summary>
public sealed class StorageFileRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "storage-file",
        DisplayName = "File Storage",
        Category = "Storage",
        Description = "Reads or writes data to a file",
        Icon = "fa-file",
        Color = "#7f8c8d",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "filePath", Label = "File Path", Type = "string", Required = true },
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "read", Label = "Read" },
                        new SelectOption { Value = "write", Label = "Write" },
                        new SelectOption { Value = "append", Label = "Append" }
                    }
                }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<FileCfg>();
        if (string.IsNullOrWhiteSpace(cfg?.FilePath))
        {
            context.Logger.Warn("File Storage: no file path configured");
            context.Emitter.Emit("output", context.Message);
            return;
        }

        // Resolve relative paths against the config directory
        var filePath = Path.GetFullPath(cfg.FilePath);
        var operation = cfg.Operation ?? "read";

        try
        {
            switch (operation.ToLowerInvariant())
            {
                case "read":
                {
                    if (!File.Exists(filePath))
                    {
                        context.Logger.Warn($"File not found: {filePath}");
                        context.Emitter.Emit("output", context.Message);
                        return;
                    }
                    var content = await File.ReadAllTextAsync(filePath, ct);
                    context.Logger.Info($"Read {content.Length} chars from {filePath}");
                    var payload = CreatePayload(content);
                    context.Emitter.Emit("output", context.Message with { Payload = payload });
                    break;
                }
                case "write":
                {
                    var data = context.Message.Payload?.ToString() ?? "";
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(filePath, data, ct);
                    context.Logger.Info($"Wrote {data.Length} chars to {filePath}");
                    context.Emitter.Emit("output", context.Message);
                    break;
                }
                case "append":
                {
                    var data = context.Message.Payload?.ToString() ?? "";
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    await File.AppendAllTextAsync(filePath, data + Environment.NewLine, ct);
                    context.Logger.Info($"Appended {data.Length} chars to {filePath}");
                    context.Emitter.Emit("output", context.Message);
                    break;
                }
                default:
                    context.Logger.Warn($"Unknown file operation: {operation}");
                    context.Emitter.Emit("output", context.Message);
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Logger.Error($"File Storage error: {ex.Message}");
            context.Emitter.EmitError(ex, context.Message);
        }
    }

    private sealed class FileCfg
    {
        public string? FilePath { get; set; }
        public string? Operation { get; set; }
    }
}

/// <summary>
/// Storage SQLite — reads/writes data to SQLite (placeholder).
/// </summary>
public sealed class StorageSqliteRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "storage-sqlite",
        DisplayName = "SQLite Storage",
        Category = "Storage",
        Description = "Reads or writes data to SQLite database",
        Icon = "fa-database",
        Color = "#7f8c8d",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "database", Label = "Database Path", Type = "string", Required = true },
                new ConfigProperty { Name = "query", Label = "SQL Query", Type = "string" }
            }
        }
    };

    public override async ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<SqliteCfg>();
        if (string.IsNullOrWhiteSpace(cfg?.Database))
        {
            context.Logger.Warn("SQLite Storage: no database path configured");
            context.Emitter.Emit("output", context.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(cfg.Query))
        {
            context.Logger.Warn("SQLite Storage: no SQL query configured");
            context.Emitter.Emit("output", context.Message);
            return;
        }

        var dbPath = Path.GetFullPath(cfg.Database);
        var connStr = $"Data Source={dbPath}";

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
            await connection.OpenAsync(ct);

            var isSelect = cfg.Query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

            if (isSelect)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = cfg.Query;
                using var reader = await cmd.ExecuteReaderAsync(ct);

                var rows = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }

                context.Logger.Info($"SQLite query returned {rows.Count} rows from {dbPath}");
                var payload = CreatePayload(rows);
                context.Emitter.Emit("output", context.Message with { Payload = payload });
            }
            else
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = cfg.Query;
                var affected = await cmd.ExecuteNonQueryAsync(ct);
                context.Logger.Info($"SQLite execute affected {affected} rows in {dbPath}");
                var payload = CreatePayload(affected);
                context.Emitter.Emit("output", context.Message with { Payload = payload });
            }
        }
        catch (Exception ex)
        {
            context.Logger.Error($"SQLite Storage error: {ex.Message}");
            context.Emitter.EmitError(ex, context.Message);
        }
    }

    private sealed class SqliteCfg
    {
        public string? Database { get; set; }
        public string? Query { get; set; }
    }
}

// ─── Helper ─────────────────────────────────────────────────────

/// <summary>
/// Shared utility for extracting numeric values from message payloads.
/// </summary>
internal static class NumericHelper
{
    /// <summary>Tolerance for floating-point equality comparisons.</summary>
    public const double FloatEpsilon = 0.0001;

    public static double Extract(MessageEnvelope message, string? property)
    {
        if (message.Payload == null) return 0;
        var prop = property ?? "value";
        if (message.Payload.Value.TryGetProperty(prop, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
            if (double.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }

    /// <summary>Evaluates whether a JSON element represents a truthy value.</summary>
    public static bool IsTruthy(JsonElement? el)
    {
        if (el == null) return false;
        return el.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => Math.Abs(el.Value.GetDouble()) >= FloatEpsilon,
            JsonValueKind.String => !string.IsNullOrEmpty(el.Value.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            _ => true
        };
    }
}
