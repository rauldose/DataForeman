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
            var inputValue = NumericHelper.ExtractInputValue(context.Message);
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
            var inputValue = NumericHelper.ExtractInputValue(context.Message);
            var timeoutSec = cfg.Timeout > 0 ? cfg.Timeout / 1000.0 : 5.0;

            var engine = new Jint.Engine(opts =>
            {
                Jint.ConstraintsOptionsExtensions.TimeoutInterval(opts, TimeSpan.FromSeconds(timeoutSec));
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

    private sealed class JsCfg { public string? Code { get; set; } public int Timeout { get; set; } = 5000; }
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

// ─── Math (Extended) ────────────────────────────────────────────

/// <summary>
/// Clamp — constrains a value between min and max bounds.
/// </summary>
public sealed class ClampRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "clamp",
        DisplayName = "Clamp",
        Category = "Math",
        Description = "Constrains a value between min and max bounds",
        Icon = "fa-compress-arrows-alt",
        Color = "#9C27B0",
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
                new ConfigProperty { Name = "min", Label = "Minimum", Type = "number" },
                new ConfigProperty { Name = "max", Label = "Maximum", Type = "number" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<ClampCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var min = cfg?.Min ?? 0;
        var max = cfg?.Max ?? 100;
        var result = Math.Min(Math.Max(val, min), max);
        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private sealed class ClampCfg { public double Min { get; set; } public double Max { get; set; } = 100; public string? Property { get; set; } }
}

/// <summary>
/// Round — rounds a value using configurable mode and precision.
/// </summary>
public sealed class RoundRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "round",
        DisplayName = "Round",
        Category = "Math",
        Description = "Rounds a value using configurable mode and precision",
        Icon = "fa-circle-dot",
        Color = "#00897B",
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
                new ConfigProperty { Name = "mode", Label = "Rounding Mode", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "round", Label = "Round" },
                        new SelectOption { Value = "floor", Label = "Floor" },
                        new SelectOption { Value = "ceil", Label = "Ceiling" },
                        new SelectOption { Value = "trunc", Label = "Truncate" }
                    }
                },
                new ConfigProperty { Name = "precision", Label = "Decimal Places", Type = "number" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<RoundCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var precision = (int)(cfg?.Precision ?? 0);
        var factor = Math.Pow(10, precision);

        var result = (cfg?.Mode ?? "round") switch
        {
            "floor" => Math.Floor(val * factor) / factor,
            "ceil" => Math.Ceiling(val * factor) / factor,
            "trunc" => Math.Truncate(val * factor) / factor,
            _ => Math.Round(val * factor) / factor
        };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private sealed class RoundCfg { public string? Mode { get; set; } public double Precision { get; set; } public string? Property { get; set; } }
}

// ─── Logic (Extended) ───────────────────────────────────────────

/// <summary>
/// Gate — conditionally passes or blocks messages.
/// </summary>
public sealed class GateRuntime : NodeRuntimeBase
{
    private JsonElement? _previousValue;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "gate",
        DisplayName = "Gate",
        Category = "Logic",
        Description = "Conditionally passes or blocks messages based on a condition input",
        Icon = "fa-door-open",
        Color = "#00BCD4",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "condition", Label = "Condition", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "output", Label = "Output", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "falseOutputMode", Label = "When False", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "null", Label = "Suppress Output" },
                        new SelectOption { Value = "previous", Label = "Emit Previous Value" }
                    }
                }
            }
        }
    };

    private JsonElement? _lastCondition;
    private MessageEnvelope? _lastInput;

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<GateCfg>();
        var portName = context.Message.SourcePort ?? "input";

        if (portName == "condition")
            _lastCondition = context.Message.Payload;
        else
            _lastInput = context.Message;

        if (_lastInput == null) return ValueTask.CompletedTask;

        var isOpen = NumericHelper.IsTruthy(_lastCondition);
        if (isOpen)
        {
            _previousValue = _lastInput.Payload;
            context.Emitter.Emit("output", _lastInput);
        }
        else
        {
            var mode = cfg?.FalseOutputMode ?? "null";
            if (mode == "previous" && _previousValue is JsonElement prev)
            {
                context.Emitter.Emit("output", _lastInput with { Payload = prev });
            }
            // "null" mode: suppress output
        }

        return ValueTask.CompletedTask;
    }

    private sealed class GateCfg { public string? FalseOutputMode { get; set; } }
}

/// <summary>
/// Merge — combines messages from two inputs using a configurable strategy.
/// </summary>
public sealed class MergeRuntime : NodeRuntimeBase
{
    private readonly Dictionary<string, object?> _inputValues = new();

    public static NodeDescriptor Descriptor => new()
    {
        Type = "merge",
        DisplayName = "Merge",
        Category = "Logic",
        Description = "Combines messages from two inputs using a configurable strategy",
        Icon = "fa-code-merge",
        Color = "#7B1FA2",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input1", Label = "Input 1", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "input2", Label = "Input 2", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "merged", Label = "Merged", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "strategy", Label = "Strategy", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "first-valid", Label = "First Valid" },
                        new SelectOption { Value = "latest", Label = "Latest" },
                        new SelectOption { Value = "min", Label = "Minimum" },
                        new SelectOption { Value = "max", Label = "Maximum" },
                        new SelectOption { Value = "average", Label = "Average" },
                        new SelectOption { Value = "sum", Label = "Sum" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<MergeCfg>();
        var portName = context.Message.SourcePort ?? "input1";
        _inputValues[portName] = context.Message.Payload;

        var strategy = cfg?.Strategy ?? "first-valid";

        object? result = strategy switch
        {
            "latest" => context.Message.Payload,
            "first-valid" => _inputValues.Values.FirstOrDefault(v => v != null),
            _ => ApplyNumericStrategy(strategy)
        };

        if (result != null)
            context.Emitter.Emit("merged", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));

        return ValueTask.CompletedTask;
    }

    private object? ApplyNumericStrategy(string strategy)
    {
        var values = _inputValues.Values
            .OfType<JsonElement>()
            .Where(e => e.ValueKind == JsonValueKind.Number || e.TryGetProperty("value", out _))
            .Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() :
                         e.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (values.Count == 0) return null;

        return strategy switch
        {
            "min" => values.Min(),
            "max" => values.Max(),
            "average" => values.Average(),
            "sum" => values.Sum(),
            _ => values.FirstOrDefault()
        };
    }

    private sealed class MergeCfg { public string? Strategy { get; set; } }
}

/// <summary>
/// State Machine — manages state transitions based on events.
/// </summary>
public sealed class StateMachineNodeRuntime : NodeRuntimeBase
{
    private string? _currentState;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "state-machine",
        DisplayName = "State Machine",
        Category = "Logic",
        Description = "Manages state transitions based on events",
        Icon = "fa-diagram-project",
        Color = "#7B1FA2",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "event", Label = "Event", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "reset", Label = "Reset", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "currentState", Label = "Current State", Direction = PortDirection.Output },
            new PortDescriptor { Name = "transition", Label = "Transition", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "initialState", Label = "Initial State", Type = "string" },
                new ConfigProperty { Name = "transitions", Label = "Transitions (source:event->target,...)", Type = "string" },
                new ConfigProperty { Name = "resetOnInvalid", Label = "Reset on Invalid", Type = "boolean" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<StateMachineCfg>();
        var initial = cfg?.InitialState ?? "idle";
        _currentState ??= initial;

        var portName = context.Message.SourcePort ?? "event";

        if (portName == "reset")
        {
            _currentState = initial;
            context.Emitter.Emit("currentState", context.Message.Derive(context.CurrentUtc, CreatePayload(new { state = _currentState })));
            return ValueTask.CompletedTask;
        }

        // Extract event name from payload
        var eventName = "unknown";
        if (context.Message.Payload is { } p)
        {
            if (p.TryGetProperty("event", out var ev))
                eventName = ev.GetString() ?? "unknown";
            else if (p.ValueKind == JsonValueKind.String)
                eventName = p.GetString() ?? "unknown";
            else if (p.TryGetProperty("value", out var vv))
                eventName = vv.ToString();
        }

        // Parse transitions: "source:event->target,source2:event2->target2"
        var transitions = ParseTransitions(cfg?.Transitions ?? "");
        var key = $"{_currentState}:{eventName}";

        if (transitions.TryGetValue(key, out var target))
        {
            var from = _currentState;
            _currentState = target;
            context.Emitter.Emit("transition", context.Message.Derive(context.CurrentUtc, CreatePayload(new { from, to = target, @event = eventName })));
            context.Emitter.Emit("currentState", context.Message.Derive(context.CurrentUtc, CreatePayload(new { state = _currentState })));
        }
        else if (cfg?.ResetOnInvalid == true)
        {
            _currentState = initial;
            context.Emitter.Emit("currentState", context.Message.Derive(context.CurrentUtc, CreatePayload(new { state = _currentState })));
        }

        return ValueTask.CompletedTask;
    }

    private static Dictionary<string, string> ParseTransitions(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Format: source:event->target
            var arrowIdx = part.IndexOf("->", StringComparison.Ordinal);
            if (arrowIdx < 0) continue;
            var left = part[..arrowIdx];
            var target = part[(arrowIdx + 2)..].Trim();
            result[left.Trim()] = target;
        }
        return result;
    }

    private sealed class StateMachineCfg { public string? InitialState { get; set; } public string? Transitions { get; set; } public bool ResetOnInvalid { get; set; } }
}

/// <summary>
/// Range Check — checks if a value falls within a configured range.
/// </summary>
public sealed class RangeCheckRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "range-check",
        DisplayName = "Range Check",
        Category = "Logic",
        Description = "Checks if a value falls within a configured range",
        Icon = "fa-ruler-combined",
        Color = "#4CAF50",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "inRange", Label = "In Range", Direction = PortDirection.Output },
            new PortDescriptor { Name = "value", Label = "Value", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "min", Label = "Minimum", Type = "number" },
                new ConfigProperty { Name = "max", Label = "Maximum", Type = "number" },
                new ConfigProperty { Name = "rangeMode", Label = "Range Mode", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "inclusive", Label = "Inclusive" },
                        new SelectOption { Value = "exclusive", Label = "Exclusive" },
                        new SelectOption { Value = "minInclusive", Label = "Min Inclusive" },
                        new SelectOption { Value = "maxInclusive", Label = "Max Inclusive" }
                    }
                },
                new ConfigProperty { Name = "outputMode", Label = "Output Mode", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "boolean", Label = "Boolean Only" },
                        new SelectOption { Value = "both", Label = "Boolean + Value" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<RangeCheckCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var min = cfg?.Min ?? 0;
        var max = cfg?.Max ?? 100;
        var mode = cfg?.RangeMode ?? "inclusive";

        var inRange = mode switch
        {
            "exclusive" => val > min && val < max,
            "minInclusive" => val >= min && val < max,
            "maxInclusive" => val > min && val <= max,
            _ => val >= min && val <= max // inclusive
        };

        context.Emitter.Emit("inRange", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = inRange })));

        if ((cfg?.OutputMode ?? "both") == "both")
            context.Emitter.Emit("value", context.Message);

        return ValueTask.CompletedTask;
    }

    private sealed class RangeCheckCfg { public double Min { get; set; } public double Max { get; set; } = 100; public string? RangeMode { get; set; } public string? OutputMode { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// Boolean Logic — applies boolean operations on two inputs.
/// </summary>
public sealed class BooleanLogicRuntime : NodeRuntimeBase
{
    private JsonElement? _lastInput1;
    private JsonElement? _lastInput2;

    public static NodeDescriptor Descriptor => new()
    {
        Type = "boolean-logic",
        DisplayName = "Boolean Logic",
        Category = "Logic",
        Description = "Applies boolean operations on two inputs",
        Icon = "fa-microchip",
        Color = "#9C27B0",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input1", Label = "Input 1", Direction = PortDirection.Input, Required = true },
            new PortDescriptor { Name = "input2", Label = "Input 2", Direction = PortDirection.Input }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "result", Label = "Result", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "and", Label = "AND" },
                        new SelectOption { Value = "or", Label = "OR" },
                        new SelectOption { Value = "xor", Label = "XOR" },
                        new SelectOption { Value = "not", Label = "NOT (Input 1 only)" },
                        new SelectOption { Value = "nand", Label = "NAND" },
                        new SelectOption { Value = "nor", Label = "NOR" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<BoolLogicCfg>();
        var portName = context.Message.SourcePort ?? "input1";

        if (portName == "input2")
            _lastInput2 = context.Message.Payload;
        else
            _lastInput1 = context.Message.Payload;

        var a = NumericHelper.IsTruthy(_lastInput1);
        var b = NumericHelper.IsTruthy(_lastInput2);
        var op = cfg?.Operation ?? "and";

        var result = op switch
        {
            "or" => a || b,
            "xor" => a ^ b,
            "not" => !a,
            "nand" => !(a && b),
            "nor" => !(a || b),
            _ => a && b // and
        };

        context.Emitter.Emit("result", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private sealed class BoolLogicCfg { public string? Operation { get; set; } }
}

// ─── Data Transform (Extended) ──────────────────────────────────

/// <summary>
/// Type Convert — converts a value to a target type.
/// </summary>
public sealed class TypeConvertRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "type-convert",
        DisplayName = "Type Convert",
        Category = "Data Transform",
        Description = "Converts a value to a target type",
        Icon = "fa-exchange-alt",
        Color = "#00BCD4",
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
                new ConfigProperty { Name = "targetType", Label = "Target Type", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "number", Label = "Number" },
                        new SelectOption { Value = "string", Label = "String" },
                        new SelectOption { Value = "boolean", Label = "Boolean" }
                    }
                },
                new ConfigProperty { Name = "onError", Label = "On Error", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "null", Label = "Emit Null" },
                        new SelectOption { Value = "original", Label = "Pass Original" },
                        new SelectOption { Value = "default", Label = "Use Default" }
                    }
                },
                new ConfigProperty { Name = "defaultValue", Label = "Default Value", Type = "string" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<TypeConvertCfg>();
        var target = cfg?.TargetType ?? "string";
        var onError = cfg?.OnError ?? "null";
        var prop = cfg?.Property ?? "value";

        object? inputVal = null;
        if (context.Message.Payload is { } p && p.TryGetProperty(prop, out var el))
            inputVal = el.ToString();
        else if (context.Message.Payload is { } pp)
            inputVal = pp.ToString();

        var raw = inputVal?.ToString() ?? "";

        try
        {
            object converted = target switch
            {
                "number" => double.Parse(raw),
                "boolean" => raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1",
                _ => raw
            };
            context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = converted })));
        }
        catch
        {
            switch (onError)
            {
                case "original":
                    context.Emitter.Emit("output", context.Message);
                    break;
                case "default":
                    context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = cfg?.DefaultValue ?? "" })));
                    break;
                default: // "null"
                    context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = (object?)null })));
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }

    private sealed class TypeConvertCfg { public string? TargetType { get; set; } public string? OnError { get; set; } public string? DefaultValue { get; set; } public string? Property { get; set; } }
}

/// <summary>
/// String Ops — performs string operations on input values.
/// </summary>
public sealed class StringOpsRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "string-ops",
        DisplayName = "String Operations",
        Category = "Data Transform",
        Description = "Performs string operations on input values",
        Icon = "fa-font",
        Color = "#795548",
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
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "uppercase", Label = "Uppercase" },
                        new SelectOption { Value = "lowercase", Label = "Lowercase" },
                        new SelectOption { Value = "trim", Label = "Trim" },
                        new SelectOption { Value = "length", Label = "Length" },
                        new SelectOption { Value = "contains", Label = "Contains" },
                        new SelectOption { Value = "replace", Label = "Replace" },
                        new SelectOption { Value = "substring", Label = "Substring" },
                        new SelectOption { Value = "split", Label = "Split" },
                        new SelectOption { Value = "concat", Label = "Concat" },
                        new SelectOption { Value = "startsWith", Label = "Starts With" },
                        new SelectOption { Value = "endsWith", Label = "Ends With" },
                        new SelectOption { Value = "reverse", Label = "Reverse" }
                    }
                },
                new ConfigProperty { Name = "searchText", Label = "Search Text", Type = "string" },
                new ConfigProperty { Name = "replaceWith", Label = "Replace With", Type = "string" },
                new ConfigProperty { Name = "startIndex", Label = "Start Index", Type = "number" },
                new ConfigProperty { Name = "endIndex", Label = "End Index", Type = "number" },
                new ConfigProperty { Name = "delimiter", Label = "Delimiter", Type = "string" },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<StringOpsCfg>();
        var prop = cfg?.Property ?? "value";
        var str = "";
        if (context.Message.Payload is { } p)
        {
            if (p.TryGetProperty(prop, out var el))
                str = el.ToString();
            else if (p.ValueKind == JsonValueKind.String)
                str = p.GetString() ?? "";
            else
                str = p.ToString();
        }

        var op = cfg?.Operation ?? "uppercase";

        object result = op switch
        {
            "uppercase" => str.ToUpperInvariant(),
            "lowercase" => str.ToLowerInvariant(),
            "trim" => str.Trim(),
            "length" => str.Length,
            "contains" => str.Contains(cfg?.SearchText ?? "", StringComparison.Ordinal),
            "replace" => str.Replace(cfg?.SearchText ?? "", cfg?.ReplaceWith ?? "", StringComparison.Ordinal),
            "substring" => SubstringOp(str, (int)(cfg?.StartIndex ?? 0), (int)(cfg?.EndIndex ?? str.Length)),
            "split" => str.Split(cfg?.Delimiter ?? ","),
            "concat" => str + (cfg?.SearchText ?? ""),
            "startsWith" => str.StartsWith(cfg?.SearchText ?? "", StringComparison.Ordinal),
            "endsWith" => str.EndsWith(cfg?.SearchText ?? "", StringComparison.Ordinal),
            "reverse" => new string(str.Reverse().ToArray()),
            _ => str
        };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private static string SubstringOp(string str, int start, int end)
    {
        start = Math.Clamp(start, 0, str.Length);
        end = Math.Clamp(end, start, str.Length);
        return str.Substring(start, end - start);
    }

    private sealed class StringOpsCfg
    {
        public string? Operation { get; set; }
        public string? SearchText { get; set; }
        public string? ReplaceWith { get; set; }
        public double StartIndex { get; set; }
        public double EndIndex { get; set; }
        public string? Delimiter { get; set; }
        public string? Property { get; set; }
    }
}

/// <summary>
/// Array Ops — performs operations on JSON arrays.
/// </summary>
public sealed class ArrayOpsRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "array-ops",
        DisplayName = "Array Operations",
        Category = "Data Transform",
        Description = "Performs operations on JSON arrays",
        Icon = "fa-layer-group",
        Color = "#1976D2",
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
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "get-element", Label = "Get Element" },
                        new SelectOption { Value = "length", Label = "Length" },
                        new SelectOption { Value = "first", Label = "First" },
                        new SelectOption { Value = "last", Label = "Last" },
                        new SelectOption { Value = "join", Label = "Join" },
                        new SelectOption { Value = "slice", Label = "Slice" },
                        new SelectOption { Value = "includes", Label = "Includes" },
                        new SelectOption { Value = "index-of", Label = "Index Of" }
                    }
                },
                new ConfigProperty { Name = "index", Label = "Index", Type = "number" },
                new ConfigProperty { Name = "separator", Label = "Separator", Type = "string" },
                new ConfigProperty { Name = "start", Label = "Start", Type = "number" },
                new ConfigProperty { Name = "end", Label = "End", Type = "number" },
                new ConfigProperty { Name = "searchValue", Label = "Search Value", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<ArrayOpsCfg>();
        var op = cfg?.Operation ?? "length";

        JsonElement arr = default;
        if (context.Message.Payload is { } p)
        {
            if (p.ValueKind == JsonValueKind.Array)
                arr = p;
            else if (p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Array)
                arr = v;
            else
            {
                // Try parsing as JSON array
                try { arr = JsonDocument.Parse(p.GetRawText()).RootElement; } catch { /* not an array */ }
            }
        }

        if (arr.ValueKind != JsonValueKind.Array)
        {
            context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = (object?)null, error = "Input is not an array" })));
            return ValueTask.CompletedTask;
        }

        var len = arr.GetArrayLength();
        object? result = op switch
        {
            "get-element" => len > 0 ? GetElementValue(arr, Math.Clamp((int)(cfg?.Index ?? 0), 0, len - 1)) : null,
            "length" => len,
            "first" => len > 0 ? GetElementValue(arr, 0) : null,
            "last" => len > 0 ? GetElementValue(arr, len - 1) : null,
            "join" => string.Join(cfg?.Separator ?? ",", Enumerable.Range(0, len).Select(i => arr[i].ToString())),
            "slice" => SliceArray(arr, (int)(cfg?.Start ?? 0), (int)(cfg?.End ?? -1)),
            "includes" => Enumerable.Range(0, len).Any(i => arr[i].ToString() == (cfg?.SearchValue ?? "")),
            "index-of" => Enumerable.Range(0, len).Cast<int?>().FirstOrDefault(i => arr[i!.Value].ToString() == (cfg?.SearchValue ?? "")) ?? -1,
            _ => len
        };

        context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        return ValueTask.CompletedTask;
    }

    private static object? GetElementValue(JsonElement arr, int index)
    {
        var el = arr[index];
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => el.ToString()
        };
    }

    private static object SliceArray(JsonElement arr, int start, int end)
    {
        var len = arr.GetArrayLength();
        if (end < 0) end = len + end + 1;
        start = Math.Clamp(start, 0, len);
        end = Math.Clamp(end, start, len);
        return Enumerable.Range(start, end - start).Select(i => arr[i].ToString()).ToArray();
    }

    private sealed class ArrayOpsCfg
    {
        public string? Operation { get; set; }
        public double Index { get; set; }
        public string? Separator { get; set; } = ",";
        public double Start { get; set; }
        public double End { get; set; } = -1;
        public string? SearchValue { get; set; }
    }
}

/// <summary>
/// JSON Ops — performs JSON-specific operations like parse, stringify, property access.
/// </summary>
public sealed class JsonOpsRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "json-ops",
        DisplayName = "JSON Operations",
        Category = "Data Transform",
        Description = "Performs JSON-specific operations like parse, stringify, and property access",
        Icon = "fa-code",
        Color = "#FF6F00",
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
                new ConfigProperty { Name = "operation", Label = "Operation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "parse", Label = "Parse" },
                        new SelectOption { Value = "stringify", Label = "Stringify" },
                        new SelectOption { Value = "get-property", Label = "Get Property" },
                        new SelectOption { Value = "has-property", Label = "Has Property" },
                        new SelectOption { Value = "keys", Label = "Keys" },
                        new SelectOption { Value = "values", Label = "Values" }
                    }
                },
                new ConfigProperty { Name = "path", Label = "Property Path (dot notation)", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<JsonOpsCfg>();
        var op = cfg?.Operation ?? "parse";

        try
        {
            object? result = op switch
            {
                "parse" => ParseOp(context),
                "stringify" => StringifyOp(context),
                "get-property" => GetPropertyOp(context, cfg?.Path ?? ""),
                "has-property" => HasPropertyOp(context, cfg?.Path ?? ""),
                "keys" => KeysOp(context),
                "values" => ValuesOp(context),
                _ => null
            };

            context.Emitter.Emit("output", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = result })));
        }
        catch (Exception ex)
        {
            context.Logger.Error($"JSON Ops error: {ex.Message}");
            context.Emitter.EmitError(ex, context.Message);
        }

        return ValueTask.CompletedTask;
    }

    private static object? ParseOp(NodeExecutionContext context)
    {
        if (context.Message.Payload is not { } p) return null;
        var raw = p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.GetRawText();
        return JsonDocument.Parse(raw).RootElement.ToString();
    }

    private static string StringifyOp(NodeExecutionContext context)
    {
        if (context.Message.Payload is not { } p) return "null";
        return p.GetRawText();
    }

    private static object? GetPropertyOp(NodeExecutionContext context, string path)
    {
        if (context.Message.Payload is not { } p || string.IsNullOrEmpty(path)) return null;
        var current = p;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.ValueKind switch
        {
            JsonValueKind.Number => current.GetDouble(),
            JsonValueKind.String => current.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => current.ToString()
        };
    }

    private static bool HasPropertyOp(NodeExecutionContext context, string path)
    {
        if (context.Message.Payload is not { } p || string.IsNullOrEmpty(path)) return false;
        var current = p;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return false;
            current = next;
        }
        return true;
    }

    private static object KeysOp(NodeExecutionContext context)
    {
        if (context.Message.Payload is not { } p || p.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();
        return p.EnumerateObject().Select(prop => prop.Name).ToArray();
    }

    private static object ValuesOp(NodeExecutionContext context)
    {
        if (context.Message.Payload is not { } p || p.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();
        return p.EnumerateObject().Select(prop => prop.Value.ToString()).ToArray();
    }

    private sealed class JsonOpsCfg { public string? Operation { get; set; } public string? Path { get; set; } }
}

/// <summary>
/// Timeline — buffers timestamped values and computes windowed aggregations.
/// </summary>
public sealed class TimelineRuntime : NodeRuntimeBase
{
    private readonly List<(double Value, DateTime Timestamp)> _buffer = new();

    public static NodeDescriptor Descriptor => new()
    {
        Type = "timeline",
        DisplayName = "Timeline",
        Category = "Data Transform",
        Description = "Buffers timestamped values and computes windowed aggregations",
        Icon = "fa-chart-line",
        Color = "#0288D1",
        InputPorts = new[]
        {
            new PortDescriptor { Name = "input", Label = "Input", Direction = PortDirection.Input, Required = true }
        },
        OutputPorts = new[]
        {
            new PortDescriptor { Name = "aggregated", Label = "Aggregated", Direction = PortDirection.Output },
            new PortDescriptor { Name = "buffer", Label = "Buffer", Direction = PortDirection.Output }
        },
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "maxEntries", Label = "Max Entries", Type = "number" },
                new ConfigProperty { Name = "windowMs", Label = "Window (ms)", Type = "number" },
                new ConfigProperty { Name = "aggregation", Label = "Aggregation", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "last", Label = "Last" },
                        new SelectOption { Value = "first", Label = "First" },
                        new SelectOption { Value = "avg", Label = "Average" },
                        new SelectOption { Value = "min", Label = "Minimum" },
                        new SelectOption { Value = "max", Label = "Maximum" },
                        new SelectOption { Value = "sum", Label = "Sum" },
                        new SelectOption { Value = "count", Label = "Count" },
                        new SelectOption { Value = "range", Label = "Range (Max - Min)" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Value Property", Type = "string" }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var cfg = context.GetConfig<TimelineCfg>();
        var val = NumericHelper.Extract(context.Message, cfg?.Property);
        var now = context.CurrentUtc;
        var maxEntries = (int)(cfg?.MaxEntries ?? 100);
        var windowMs = cfg?.WindowMs ?? 0;
        var aggregation = cfg?.Aggregation ?? "last";

        _buffer.Add((val, now));

        // Prune by time window
        if (windowMs > 0)
        {
            var cutoff = now.AddMilliseconds(-windowMs);
            _buffer.RemoveAll(e => e.Timestamp < cutoff);
        }

        // Prune by max entries
        while (_buffer.Count > maxEntries)
            _buffer.RemoveAt(0);

        var values = _buffer.Select(e => e.Value).ToList();
        double aggregated = 0;
        if (values.Count > 0)
        {
            aggregated = aggregation switch
            {
                "first" => values[0],
                "avg" => values.Average(),
                "min" => values.Min(),
                "max" => values.Max(),
                "sum" => values.Sum(),
                "count" => values.Count,
                "range" => values.Max() - values.Min(),
                _ => values[^1] // last
            };
        }

        context.Emitter.Emit("aggregated", context.Message.Derive(context.CurrentUtc, CreatePayload(new { value = aggregated, count = _buffer.Count })));
        context.Emitter.Emit("buffer", context.Message.Derive(context.CurrentUtc, CreatePayload(
            _buffer.Select(e => new { e.Value, timestamp = e.Timestamp.ToString("o") }).ToArray())));

        return ValueTask.CompletedTask;
    }

    private sealed class TimelineCfg
    {
        public double MaxEntries { get; set; } = 100;
        public double WindowMs { get; set; }
        public string? Aggregation { get; set; }
        public string? Property { get; set; }
    }
}

// ─── Utility (Extended) ─────────────────────────────────────────

/// <summary>
/// Comment — a visual-only node with no execution logic.
/// </summary>
public sealed class CommentRuntime : NodeRuntimeBase
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "comment",
        DisplayName = "Comment",
        Category = "Utility",
        Description = "A visual-only comment node for documentation purposes",
        Icon = "fa-comment",
        Color = "#FFC107",
        InputPorts = Array.Empty<PortDescriptor>(),
        OutputPorts = Array.Empty<PortDescriptor>(),
        ConfigSchema = new NodeConfigSchema
        {
            Properties = new[]
            {
                new ConfigProperty { Name = "text", Label = "Comment Text", Type = "string" },
                new ConfigProperty { Name = "fontSize", Label = "Font Size", Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "small", Label = "Small" },
                        new SelectOption { Value = "medium", Label = "Medium" },
                        new SelectOption { Value = "large", Label = "Large" }
                    }
                }
            }
        }
    };

    public override ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
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

    /// <summary>Extracts a typed input value from a message payload for script nodes.</summary>
    public static object? ExtractInputValue(MessageEnvelope message)
    {
        if (!message.Payload.HasValue) return null;
        var p = message.Payload.Value;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String) return p.GetString();
        if (p.TryGetProperty("value", out var vp) && vp.ValueKind == JsonValueKind.Number) return vp.GetDouble();
        return p.ToString();
    }
}
