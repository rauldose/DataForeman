using DataForeman.Shared.Models;

namespace DataForeman.App.Services;

/// <summary>
/// Registry for flow node plugins. Provides metadata-driven node definitions
/// that enable extensible node types for the flow editor.
/// </summary>
public class NodePluginRegistry
{
    private readonly Dictionary<string, NodePluginDefinition> _plugins = new();
    private readonly Dictionary<string, List<NodePluginDefinition>> _byCategory = new();
    private readonly List<string> _categoryOrder = new();
    private readonly object _lock = new();

    public IReadOnlyCollection<NodePluginDefinition> All
    {
        get { lock (_lock) { return _plugins.Values.ToList().AsReadOnly(); } }
    }

    public IReadOnlyList<string> Categories
    {
        get { lock (_lock) { return _categoryOrder.AsReadOnly(); } }
    }

    public NodePluginRegistry()
    {
        RegisterBuiltInPlugins();
    }

    public void Register(NodePluginDefinition plugin)
    {
        lock (_lock)
        {
            _plugins[plugin.Id] = plugin;
            if (!_byCategory.ContainsKey(plugin.Category))
            {
                _byCategory[plugin.Category] = new List<NodePluginDefinition>();
                _categoryOrder.Add(plugin.Category);
            }
            var existing = _byCategory[plugin.Category].FindIndex(p => p.Id == plugin.Id);
            if (existing >= 0)
                _byCategory[plugin.Category][existing] = plugin;
            else
                _byCategory[plugin.Category].Add(plugin);
        }
    }

    public NodePluginDefinition? Get(string id)
    {
        var baseId = id.Contains("-instance-") ? id.Split("-instance-")[0] : id;
        lock (_lock)
        {
            return _plugins.TryGetValue(baseId, out var plugin) ? plugin : null;
        }
    }

    public IReadOnlyList<NodePluginDefinition> GetByCategory(string category)
    {
        lock (_lock)
        {
            return _byCategory.TryGetValue(category, out var plugins)
                ? plugins.ToList().AsReadOnly()
                : Array.Empty<NodePluginDefinition>();
        }
    }

    private void RegisterBuiltInPlugins()
    {
        // === TRIGGER NODES ===
        Register(new NodePluginDefinition
        {
            Id = "trigger-manual",
            Name = "Manual Trigger",
            ShortLabel = "Manual",
            Category = "Triggers",
            Description = "Fires when manually activated",
            Icon = "fa-solid fa-hand-pointer",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "runOnDeploy", Label = "Run on Deploy", Type = PropertyType.Boolean, DefaultValue = "false", Group = "Behavior" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "trigger-schedule",
            Name = "Schedule Trigger",
            ShortLabel = "Schedule",
            Category = "Triggers",
            Description = "Fires on a time-based schedule",
            Icon = "fa-solid fa-clock",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "interval", Label = "Interval (seconds)", Type = PropertyType.Integer, DefaultValue = "60", Min = 1, Max = 86400, Group = "Schedule" },
                new() { Key = "cron", Label = "Cron Expression", Type = PropertyType.Cron, Placeholder = "0 */5 * * * *", Group = "Schedule", Advanced = true }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "trigger-tag",
            Name = "Tag Change Trigger",
            ShortLabel = "On Change",
            Category = "Triggers",
            Description = "Fires when a tag value changes",
            Icon = "fa-solid fa-bolt",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Required = true, Group = "Tag" },
                new() { Key = "triggerMode", Label = "Trigger Mode", Type = PropertyType.Select, DefaultValue = "Any Change", Options = new() { new() { Value = "Any Change", Label = "Any Change" }, new() { Value = "Rising Edge", Label = "Rising Edge" }, new() { Value = "Falling Edge", Label = "Falling Edge" } }, Group = "Tag" }
            }
        });

        // === TAG I/O NODES ===
        Register(new NodePluginDefinition
        {
            Id = "tag-input",
            Name = "Tag Input",
            ShortLabel = "Read Tag",
            Category = "Tags",
            Description = "Reads a value from a tag",
            Icon = "fa-solid fa-right-to-bracket",
            Color = "#3b82f6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Required = true, Group = "Tag" },
                new() { Key = "dataType", Label = "Data Type", Type = PropertyType.Select, DefaultValue = "Float", Options = new() { new() { Value = "Float", Label = "Float" }, new() { Value = "Integer", Label = "Integer" }, new() { Value = "Boolean", Label = "Boolean" }, new() { Value = "String", Label = "String" } }, Group = "Tag" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "tag-output",
            Name = "Tag Output",
            ShortLabel = "Write Tag",
            Category = "Tags",
            Description = "Writes a value to a tag",
            Icon = "fa-solid fa-right-from-bracket",
            Color = "#f59e0b",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Required = true, Group = "Tag" },
                new() { Key = "dataType", Label = "Data Type", Type = PropertyType.Select, DefaultValue = "Float", Options = new() { new() { Value = "Float", Label = "Float" }, new() { Value = "Integer", Label = "Integer" }, new() { Value = "Boolean", Label = "Boolean" }, new() { Value = "String", Label = "String" } }, Group = "Tag" }
            }
        });

        // === MATH NODES ===
        Register(new NodePluginDefinition
        {
            Id = "math-add",
            Name = "Add",
            ShortLabel = "Add",
            Category = "Math",
            Description = "Adds two values together",
            Icon = "fa-solid fa-plus",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "math-subtract",
            Name = "Subtract",
            ShortLabel = "Subtract",
            Category = "Math",
            Description = "Subtracts second value from first",
            Icon = "fa-solid fa-minus",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "math-multiply",
            Name = "Multiply",
            ShortLabel = "Multiply",
            Category = "Math",
            Description = "Multiplies two values",
            Icon = "fa-solid fa-xmark",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "1", Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "math-divide",
            Name = "Divide",
            ShortLabel = "Divide",
            Category = "Math",
            Description = "Divides first value by second",
            Icon = "fa-solid fa-divide",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "1", Min = 0.001, Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "math-scale",
            Name = "Scale",
            ShortLabel = "Scale",
            Category = "Math",
            Description = "Scales input from one range to another",
            Icon = "fa-solid fa-arrows-left-right-to-line",
            Color = "#a855f7",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "inMin", Label = "Input Min", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Input Range" },
                new() { Key = "inMax", Label = "Input Max", Type = PropertyType.Decimal, DefaultValue = "100", Group = "Input Range" },
                new() { Key = "outMin", Label = "Output Min", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Output Range" },
                new() { Key = "outMax", Label = "Output Max", Type = PropertyType.Decimal, DefaultValue = "1", Group = "Output Range" }
            }
        });

        // === LOGIC NODES ===
        Register(new NodePluginDefinition
        {
            Id = "logic-branch",
            Name = "Branch",
            ShortLabel = "Branch",
            Category = "Logic",
            Description = "Routes flow based on condition",
            Icon = "fa-solid fa-code-branch",
            Color = "#6b7280",
            InputCount = 1,
            OutputCount = 2,
            Properties = new()
            {
                new() { Key = "condition", Label = "Condition", Type = PropertyType.Select, DefaultValue = "truthy", Options = new() { new() { Value = "truthy", Label = "If Truthy" }, new() { Value = "equals", Label = "Equals Value" }, new() { Value = "greater", Label = "Greater Than" }, new() { Value = "less", Label = "Less Than" } }, Group = "Condition" },
                new() { Key = "threshold", Label = "Compare Value", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Condition" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "logic-compare",
            Name = "Compare",
            ShortLabel = "Compare",
            Category = "Logic",
            Description = "Compares two values",
            Icon = "fa-solid fa-not-equal",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = ">", Options = new() { new() { Value = ">", Label = "Greater Than" }, new() { Value = "<", Label = "Less Than" }, new() { Value = "==", Label = "Equal" }, new() { Value = ">=", Label = "Greater Or Equal" }, new() { Value = "<=", Label = "Less Or Equal" } }, Group = "Operation" },
                new() { Key = "threshold", Label = "Threshold (if no input B)", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "logic-and",
            Name = "AND Gate",
            ShortLabel = "AND",
            Category = "Logic",
            Description = "Outputs true only if all inputs are true",
            Icon = "fa-solid fa-circle-nodes",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1
        });

        Register(new NodePluginDefinition
        {
            Id = "logic-or",
            Name = "OR Gate",
            ShortLabel = "OR",
            Category = "Logic",
            Description = "Outputs true if any input is true",
            Icon = "fa-solid fa-circle-plus",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1
        });

        // === OUTPUT NODES ===
        Register(new NodePluginDefinition
        {
            Id = "output-log",
            Name = "Debug Log",
            ShortLabel = "Log",
            Category = "Output",
            Description = "Logs values for debugging",
            Icon = "fa-solid fa-terminal",
            Color = "#ef4444",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "message", Label = "Message Template", Type = PropertyType.TextArea, DefaultValue = "Value: {{value}}", Group = "Message" },
                new() { Key = "logLevel", Label = "Log Level", Type = PropertyType.Select, DefaultValue = "Info", Options = new() { new() { Value = "Debug", Label = "Debug" }, new() { Value = "Info", Label = "Info" }, new() { Value = "Warning", Label = "Warning" }, new() { Value = "Error", Label = "Error" } }, Group = "Message" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "output-notification",
            Name = "Notification",
            ShortLabel = "Alert",
            Category = "Output",
            Description = "Sends notifications/alerts",
            Icon = "fa-solid fa-bell",
            Color = "#ec4899",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "message", Label = "Message Template", Type = PropertyType.TextArea, Placeholder = "Alert: {{value}} detected", Group = "Message" },
                new() { Key = "severity", Label = "Severity", Type = PropertyType.Select, DefaultValue = "Info", Options = new() { new() { Value = "Info", Label = "Info" }, new() { Value = "Warning", Label = "Warning" }, new() { Value = "Critical", Label = "Critical" } }, Group = "Message" }
            }
        });

        // === UTILITY NODES ===
        Register(new NodePluginDefinition
        {
            Id = "util-delay",
            Name = "Delay",
            ShortLabel = "Delay",
            Category = "Utility",
            Description = "Delays the flow execution",
            Icon = "fa-solid fa-hourglass-half",
            Color = "#64748b",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "delay", Label = "Delay (ms)", Type = PropertyType.Integer, DefaultValue = "1000", Min = 0, Max = 60000, Group = "Timing" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "util-filter",
            Name = "Filter",
            ShortLabel = "Filter",
            Category = "Utility",
            Description = "Filters out values based on condition",
            Icon = "fa-solid fa-filter",
            Color = "#64748b",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "condition", Label = "Pass If", Type = PropertyType.Select, DefaultValue = "changed", Options = new() { new() { Value = "changed", Label = "Value Changed" }, new() { Value = "nonzero", Label = "Non-Zero" }, new() { Value = "valid", Label = "Valid (not null)" } }, Group = "Filter" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "util-constant",
            Name = "Constant",
            ShortLabel = "Const",
            Category = "Utility",
            Description = "Outputs a constant value",
            Icon = "fa-solid fa-hashtag",
            Color = "#64748b",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "value", Label = "Value", Type = PropertyType.Text, DefaultValue = "0", Group = "Value" },
                new() { Key = "dataType", Label = "Data Type", Type = PropertyType.Select, DefaultValue = "Float", Options = new() { new() { Value = "Float", Label = "Float" }, new() { Value = "Integer", Label = "Integer" }, new() { Value = "Boolean", Label = "Boolean" }, new() { Value = "String", Label = "String" } }, Group = "Value" }
            }
        });
    }
}
