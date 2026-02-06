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

        // === SCRIPT NODES ===
        Register(new NodePluginDefinition
        {
            Id = "script-csharp",
            Name = "C# Script",
            ShortLabel = "C#",
            Category = "Scripts",
            Description = "Execute C# code with full Roslyn support. Define classes, helper methods, use Regex, LINQ, async patterns, and more. Use the template dropdown and API reference in the editor.",
            Icon = "fa-solid fa-file-code",
            Color = "#178600",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new()
                {
                    Key = "code",
                    Label = "C# Code",
                    Type = PropertyType.Code,
                    DefaultValue = "// Use the template dropdown above to get started\n// or write any C# code — classes, methods, LINQ, Regex, etc.\n\nvar value = ReadTagDouble(\"Connection/TagName\");\nLog($\"Value: {value}\");\nreturn value;",
                    HelpText = "Full C# scripting via Roslyn. Supports classes, local functions, LINQ, Regex, async. Click the book icon for API reference.",
                    Group = "Script",
                    Order = 0
                },
                new()
                {
                    Key = "timeout",
                    Label = "Timeout (ms)",
                    Type = PropertyType.Integer,
                    DefaultValue = "10000",
                    Min = 100,
                    Max = 60000,
                    HelpText = "Maximum execution time before the script is cancelled",
                    Group = "Settings",
                    Order = 1
                },
                new()
                {
                    Key = "onError",
                    Label = "On Error",
                    Type = PropertyType.Select,
                    DefaultValue = "stop",
                    Options = new()
                    {
                        new() { Value = "stop", Label = "Stop flow" },
                        new() { Value = "continue", Label = "Continue with null" }
                    },
                    HelpText = "What to do when the script throws an exception",
                    Group = "Settings",
                    Order = 2
                }
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

        // === COMMUNICATION NODES (Node-RED style) ===
        Register(new NodePluginDefinition
        {
            Id = "mqtt-in",
            Name = "MQTT Subscribe",
            ShortLabel = "MQTT In",
            Category = "Communication",
            Description = "Subscribe to MQTT topic",
            Icon = "fa-solid fa-arrow-right-to-bracket",
            Color = "#9333ea",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "broker", Label = "Broker URL", Type = PropertyType.Text, DefaultValue = "localhost:1883", Required = true, Group = "Connection" },
                new() { Key = "topic", Label = "Topic", Type = PropertyType.Text, Placeholder = "sensors/+/temperature", Required = true, Group = "Subscription" },
                new() { Key = "qos", Label = "QoS", Type = PropertyType.Select, DefaultValue = "0", Options = new() { new() { Value = "0", Label = "0 - At most once" }, new() { Value = "1", Label = "1 - At least once" }, new() { Value = "2", Label = "2 - Exactly once" } }, Group = "Subscription" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "mqtt-out",
            Name = "MQTT Publish",
            ShortLabel = "MQTT Out",
            Category = "Communication",
            Description = "Publish to MQTT topic",
            Icon = "fa-solid fa-arrow-right-from-bracket",
            Color = "#9333ea",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "broker", Label = "Broker URL", Type = PropertyType.Text, DefaultValue = "localhost:1883", Required = true, Group = "Connection" },
                new() { Key = "topic", Label = "Topic", Type = PropertyType.Text, Placeholder = "output/values", Required = true, Group = "Publishing" },
                new() { Key = "qos", Label = "QoS", Type = PropertyType.Select, DefaultValue = "0", Options = new() { new() { Value = "0", Label = "0 - At most once" }, new() { Value = "1", Label = "1 - At least once" }, new() { Value = "2", Label = "2 - Exactly once" } }, Group = "Publishing" },
                new() { Key = "retain", Label = "Retain", Type = PropertyType.Boolean, DefaultValue = "false", Group = "Publishing" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "http-request",
            Name = "HTTP Request",
            ShortLabel = "HTTP",
            Category = "Communication",
            Description = "Make HTTP requests",
            Icon = "fa-solid fa-globe",
            Color = "#0ea5e9",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "method", Label = "Method", Type = PropertyType.Select, DefaultValue = "GET", Options = new() { new() { Value = "GET", Label = "GET" }, new() { Value = "POST", Label = "POST" }, new() { Value = "PUT", Label = "PUT" }, new() { Value = "DELETE", Label = "DELETE" } }, Group = "Request" },
                new() { Key = "url", Label = "URL", Type = PropertyType.Text, Placeholder = "https://api.example.com/data", Required = true, Group = "Request" },
                new() { Key = "headers", Label = "Headers (JSON)", Type = PropertyType.TextArea, DefaultValue = "{}", Group = "Request", Advanced = true },
                new() { Key = "timeout", Label = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = "5000", Min = 1000, Max = 60000, Group = "Request" }
            }
        });

        // === FUNCTION NODES (Node-RED style) ===
        Register(new NodePluginDefinition
        {
            Id = "func-javascript",
            Name = "Function",
            ShortLabel = "Function",
            Category = "Function",
            Description = "Custom JavaScript function",
            Icon = "fa-solid fa-code",
            Color = "#f97316",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "code", Label = "Function Code", Type = PropertyType.Code, DefaultValue = "// msg.payload contains the input\nreturn msg;", HelpText = "Write JavaScript code. Input is 'msg' object.", Group = "Code" },
                new() { Key = "outputs", Label = "Outputs", Type = PropertyType.Integer, DefaultValue = "1", Min = 1, Max = 10, Group = "Settings" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "func-template",
            Name = "Template",
            ShortLabel = "Template",
            Category = "Function",
            Description = "Template string with mustache syntax",
            Icon = "fa-solid fa-file-code",
            Color = "#f97316",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "template", Label = "Template", Type = PropertyType.TextArea, DefaultValue = "Value: {{payload}}", HelpText = "Use {{property}} for substitution", Group = "Template" },
                new() { Key = "outputFormat", Label = "Output Format", Type = PropertyType.Select, DefaultValue = "text", Options = new() { new() { Value = "text", Label = "Plain Text" }, new() { Value = "json", Label = "JSON" } }, Group = "Output" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "func-switch",
            Name = "Switch",
            ShortLabel = "Switch",
            Category = "Function",
            Description = "Route messages based on property",
            Icon = "fa-solid fa-code-merge",
            Color = "#f97316",
            InputCount = 1,
            OutputCount = 3,
            Properties = new()
            {
                new() { Key = "property", Label = "Property", Type = PropertyType.Text, DefaultValue = "payload", Group = "Routing" },
                new() { Key = "rules", Label = "Rules (JSON)", Type = PropertyType.TextArea, DefaultValue = "[{\"op\":\">\",\"val\":50},{\"op\":\"<=\",\"val\":50}]", HelpText = "Define routing rules", Group = "Routing", Advanced = true }
            }
        });

        // === DATA PROCESSING NODES ===
        Register(new NodePluginDefinition
        {
            Id = "data-aggregate",
            Name = "Aggregate",
            ShortLabel = "Aggregate",
            Category = "Data",
            Description = "Aggregate values over time",
            Icon = "fa-solid fa-layer-group",
            Color = "#14b8a6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "avg", Options = new() { new() { Value = "avg", Label = "Average" }, new() { Value = "sum", Label = "Sum" }, new() { Value = "min", Label = "Minimum" }, new() { Value = "max", Label = "Maximum" }, new() { Value = "count", Label = "Count" } }, Group = "Aggregation" },
                new() { Key = "windowSize", Label = "Window Size", Type = PropertyType.Integer, DefaultValue = "10", Min = 1, Max = 1000, Group = "Aggregation" },
                new() { Key = "windowType", Label = "Window Type", Type = PropertyType.Select, DefaultValue = "count", Options = new() { new() { Value = "count", Label = "Count" }, new() { Value = "time", Label = "Time (seconds)" } }, Group = "Aggregation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "data-smooth",
            Name = "Smooth",
            ShortLabel = "Smooth",
            Category = "Data",
            Description = "Smooth/filter noisy signals",
            Icon = "fa-solid fa-wave-square",
            Color = "#14b8a6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "algorithm", Label = "Algorithm", Type = PropertyType.Select, DefaultValue = "ema", Options = new() { new() { Value = "ema", Label = "Exponential Moving Avg" }, new() { Value = "sma", Label = "Simple Moving Avg" }, new() { Value = "median", Label = "Median Filter" } }, Group = "Filter" },
                new() { Key = "factor", Label = "Smoothing Factor", Type = PropertyType.Decimal, DefaultValue = "0.2", Min = 0.01, Max = 1.0, HelpText = "For EMA: 0.1=slow, 0.9=fast", Group = "Filter" },
                new() { Key = "windowSize", Label = "Window Size", Type = PropertyType.Integer, DefaultValue = "5", Min = 2, Max = 100, Group = "Filter" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "data-deadband",
            Name = "Deadband",
            ShortLabel = "Deadband",
            Category = "Data",
            Description = "Suppress small value changes",
            Icon = "fa-solid fa-grip-lines",
            Color = "#14b8a6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "threshold", Label = "Threshold", Type = PropertyType.Decimal, DefaultValue = "0.5", Min = 0, Group = "Deadband" },
                new() { Key = "type", Label = "Type", Type = PropertyType.Select, DefaultValue = "absolute", Options = new() { new() { Value = "absolute", Label = "Absolute" }, new() { Value = "percentage", Label = "Percentage" } }, Group = "Deadband" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "data-rateofchange",
            Name = "Rate of Change",
            ShortLabel = "ROC",
            Category = "Data",
            Description = "Calculate rate of change",
            Icon = "fa-solid fa-chart-line",
            Color = "#14b8a6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "unit", Label = "Time Unit", Type = PropertyType.Select, DefaultValue = "second", Options = new() { new() { Value = "second", Label = "Per Second" }, new() { Value = "minute", Label = "Per Minute" }, new() { Value = "hour", Label = "Per Hour" } }, Group = "Calculation" }
            }
        });

        // === CONTEXT NODES (Internal Tags) ===
        Register(new NodePluginDefinition
        {
            Id = "context-get",
            Name = "Context Get",
            ShortLabel = "Get",
            Category = "Context",
            Description = "Read value from internal tag context (global, flow, or node scope)",
            Icon = "fa-solid fa-download",
            Color = "#16a085",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "scope", Label = "Scope", Type = PropertyType.Select, DefaultValue = "global", Required = true, Options = new() { new() { Value = "global", Label = "Global" }, new() { Value = "flow", Label = "Flow" }, new() { Value = "node", Label = "Node" } }, Group = "Context" },
                new() { Key = "key", Label = "Key", Type = PropertyType.Text, Required = true, Placeholder = "myVariable", Group = "Context" },
                new() { Key = "defaultValue", Label = "Default Value", Type = PropertyType.Text, Placeholder = "Default if not found", Group = "Context" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "context-set",
            Name = "Context Set",
            ShortLabel = "Set",
            Category = "Context",
            Description = "Write value to internal tag context (global, flow, or node scope)",
            Icon = "fa-solid fa-upload",
            Color = "#16a085",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "scope", Label = "Scope", Type = PropertyType.Select, DefaultValue = "global", Required = true, Options = new() { new() { Value = "global", Label = "Global" }, new() { Value = "flow", Label = "Flow" }, new() { Value = "node", Label = "Node" } }, Group = "Context" },
                new() { Key = "key", Label = "Key", Type = PropertyType.Text, Required = true, Placeholder = "myVariable", Group = "Context" },
                new() { Key = "valueSource", Label = "Value From", Type = PropertyType.Select, DefaultValue = "payload", Options = new() { new() { Value = "payload", Label = "Message Payload" }, new() { Value = "property", Label = "Payload Property" }, new() { Value = "static", Label = "Static Value" } }, Group = "Value" },
                new() { Key = "property", Label = "Property Name", Type = PropertyType.Text, Placeholder = "value", Group = "Value" },
                new() { Key = "staticValue", Label = "Static Value", Type = PropertyType.Text, Placeholder = "Enter value", Group = "Value" }
            }
        });

        // === TIMER/INJECT NODES ===
        Register(new NodePluginDefinition
        {
            Id = "inject-timer",
            Name = "Timer/Inject",
            ShortLabel = "Inject",
            Category = "Triggers",
            Description = "Inject values at intervals",
            Icon = "fa-solid fa-syringe",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "payload", Label = "Payload", Type = PropertyType.Text, DefaultValue = "1", Group = "Inject" },
                new() { Key = "payloadType", Label = "Payload Type", Type = PropertyType.Select, DefaultValue = "num", Options = new() { new() { Value = "num", Label = "Number" }, new() { Value = "str", Label = "String" }, new() { Value = "bool", Label = "Boolean" }, new() { Value = "timestamp", Label = "Timestamp" } }, Group = "Inject" },
                new() { Key = "repeat", Label = "Repeat Interval (sec)", Type = PropertyType.Integer, DefaultValue = "0", Min = 0, HelpText = "0 = no repeat", Group = "Timing" },
                new() { Key = "once", Label = "Inject Once at Start", Type = PropertyType.Boolean, DefaultValue = "false", Group = "Timing" }
            }
        });

        // === DEBUG NODE ===
        Register(new NodePluginDefinition
        {
            Id = "debug-sidebar",
            Name = "Debug",
            ShortLabel = "Debug",
            Category = "Output",
            Description = "Show messages in debug sidebar",
            Icon = "fa-solid fa-bug",
            Color = "#22c55e",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "active", Label = "Active", Type = PropertyType.Boolean, DefaultValue = "true", Group = "Settings" },
                new() { Key = "console", Label = "Also Log to Console", Type = PropertyType.Boolean, DefaultValue = "false", Group = "Settings" },
                new() { Key = "complete", Label = "Output", Type = PropertyType.Select, DefaultValue = "payload", Options = new() { new() { Value = "payload", Label = "msg.payload" }, new() { Value = "complete", Label = "Complete msg object" } }, Group = "Settings" }
            }
        });

        // === LINK NODES (for connecting flows) ===
        Register(new NodePluginDefinition
        {
            Id = "link-in",
            Name = "Link In",
            ShortLabel = "Link In",
            Category = "Flow",
            Description = "Receive from link-out nodes",
            Icon = "fa-solid fa-arrow-turn-down",
            Color = "#6366f1",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "name", Label = "Link Name", Type = PropertyType.Text, Required = true, Group = "Link" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "link-out",
            Name = "Link Out",
            ShortLabel = "Link Out",
            Category = "Flow",
            Description = "Send to link-in nodes",
            Icon = "fa-solid fa-arrow-turn-up",
            Color = "#6366f1",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "name", Label = "Link Name", Type = PropertyType.Text, Required = true, Group = "Link" },
                new() { Key = "mode", Label = "Mode", Type = PropertyType.Select, DefaultValue = "link", Options = new() { new() { Value = "link", Label = "Send to link node" }, new() { Value = "return", Label = "Return to calling node" } }, Group = "Link" }
            }
        });

        // === STORAGE NODES ===
        Register(new NodePluginDefinition
        {
            Id = "storage-file",
            Name = "File Write",
            ShortLabel = "File",
            Category = "Storage",
            Description = "Write data to file",
            Icon = "fa-solid fa-file-export",
            Color = "#eab308",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "filename", Label = "Filename", Type = PropertyType.Text, Placeholder = "/data/output.csv", Required = true, Group = "File" },
                new() { Key = "appendNewline", Label = "Append Newline", Type = PropertyType.Boolean, DefaultValue = "true", Group = "File" },
                new() { Key = "createDir", Label = "Create Directory", Type = PropertyType.Boolean, DefaultValue = "true", Group = "File" },
                new() { Key = "overwrite", Label = "Overwrite File", Type = PropertyType.Select, DefaultValue = "append", Options = new() { new() { Value = "append", Label = "Append" }, new() { Value = "overwrite", Label = "Overwrite" } }, Group = "File" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "storage-sqlite",
            Name = "SQLite",
            ShortLabel = "SQLite",
            Category = "Storage",
            Description = "Store/retrieve from SQLite database",
            Icon = "fa-solid fa-database",
            Color = "#eab308",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "database", Label = "Database Path", Type = PropertyType.Text, DefaultValue = "/data/history.db", Required = true, Group = "Database" },
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "insert", Options = new() { new() { Value = "insert", Label = "Insert" }, new() { Value = "query", Label = "Query" }, new() { Value = "batch", Label = "Batch Insert" } }, Group = "Operation" },
                new() { Key = "table", Label = "Table Name", Type = PropertyType.Text, DefaultValue = "history", Group = "Database" },
                new() { Key = "query", Label = "SQL Query", Type = PropertyType.TextArea, Placeholder = "SELECT * FROM history WHERE timestamp > ?", Group = "Operation", Advanced = true }
            }
        });

        // === SUBFLOW I/O NODES ===
        // These are special nodes only shown when editing a subflow
        Register(new NodePluginDefinition
        {
            Id = "subflow-input",
            Name = "Subflow Input",
            ShortLabel = "Input",
            Category = "Subflow I/O",
            Description = "Entry point for data into the subflow",
            Icon = "fa-solid fa-right-to-bracket",
            Color = "#6b7280",
            InputCount = 0,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "name", Label = "Input Name", Type = PropertyType.Text, DefaultValue = "input", Group = "Definition" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "subflow-output",
            Name = "Subflow Output",
            ShortLabel = "Output",
            Category = "Subflow I/O",
            Description = "Exit point for data from the subflow",
            Icon = "fa-solid fa-right-from-bracket",
            Color = "#6b7280",
            InputCount = 1,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "outputName", Label = "Output Name", Type = PropertyType.Text, DefaultValue = "output", Group = "Definition" }
            }
        });

        // === MATH (EXTENDED) NODES ===
        Register(new NodePluginDefinition
        {
            Id = "clamp",
            Name = "Clamp",
            ShortLabel = "Clamp",
            Category = "Math",
            Description = "Limits a numeric value to a min/max range",
            Icon = "fa-solid fa-compress-arrows-alt",
            Color = "#9C27B0",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "min", Label = "Minimum", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Range" },
                new() { Key = "max", Label = "Maximum", Type = PropertyType.Decimal, DefaultValue = "100", Group = "Range" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "round",
            Name = "Round",
            ShortLabel = "Round",
            Category = "Math",
            Description = "Rounds a numeric value with configurable precision and mode",
            Icon = "fa-solid fa-circle-dot",
            Color = "#00897B",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "mode", Label = "Mode", Type = PropertyType.Select, DefaultValue = "round", Options = new() { new() { Value = "round", Label = "Round" }, new() { Value = "floor", Label = "Floor" }, new() { Value = "ceil", Label = "Ceiling" }, new() { Value = "trunc", Label = "Truncate" } }, Group = "Settings" },
                new() { Key = "precision", Label = "Decimal Places", Type = PropertyType.Integer, DefaultValue = "0", Min = -10, Max = 15, Group = "Settings" }
            }
        });

        // === LOGIC (EXTENDED) NODES ===
        Register(new NodePluginDefinition
        {
            Id = "gate",
            Name = "Gate",
            ShortLabel = "Gate",
            Category = "Logic",
            Description = "Passes data when condition is true, blocks or holds when false",
            Icon = "fa-solid fa-door-open",
            Color = "#00BCD4",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "falseOutputMode", Label = "When False", Type = PropertyType.Select, DefaultValue = "null", Options = new() { new() { Value = "null", Label = "Block (null)" }, new() { Value = "previous", Label = "Hold Previous" } }, Group = "Behavior" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "merge",
            Name = "Merge",
            ShortLabel = "Merge",
            Category = "Logic",
            Description = "Combines multiple inputs using various strategies",
            Icon = "fa-solid fa-code-merge",
            Color = "#7B1FA2",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "strategy", Label = "Strategy", Type = PropertyType.Select, DefaultValue = "first-valid", Options = new() { new() { Value = "first-valid", Label = "First Valid" }, new() { Value = "latest", Label = "Latest" }, new() { Value = "min", Label = "Minimum" }, new() { Value = "max", Label = "Maximum" }, new() { Value = "average", Label = "Average" }, new() { Value = "sum", Label = "Sum" } }, Group = "Strategy" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "state-machine",
            Name = "State Machine",
            ShortLabel = "FSM",
            Category = "Logic",
            Description = "Finite state machine with event-driven transitions",
            Icon = "fa-solid fa-diagram-project",
            Color = "#7B1FA2",
            InputCount = 2,
            OutputCount = 2,
            Properties = new()
            {
                new() { Key = "initialState", Label = "Initial State", Type = PropertyType.Text, DefaultValue = "idle", Group = "States" },
                new() { Key = "transitions", Label = "Transitions", Type = PropertyType.Text, DefaultValue = "idle:start->running,running:stop->idle", HelpText = "Format: source:event->target, comma-separated", Group = "States" },
                new() { Key = "resetOnInvalid", Label = "Reset on Invalid", Type = PropertyType.Boolean, DefaultValue = "false", Group = "Behavior" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "range-check",
            Name = "Range Check",
            ShortLabel = "Range",
            Category = "Logic",
            Description = "Checks if a value falls within a min/max range",
            Icon = "fa-solid fa-ruler-combined",
            Color = "#4CAF50",
            InputCount = 1,
            OutputCount = 2,
            Properties = new()
            {
                new() { Key = "min", Label = "Minimum", Type = PropertyType.Decimal, DefaultValue = "0", Group = "Range" },
                new() { Key = "max", Label = "Maximum", Type = PropertyType.Decimal, DefaultValue = "100", Group = "Range" },
                new() { Key = "rangeMode", Label = "Range Mode", Type = PropertyType.Select, DefaultValue = "inclusive", Options = new() { new() { Value = "inclusive", Label = "[min, max]" }, new() { Value = "exclusive", Label = "(min, max)" }, new() { Value = "minInclusive", Label = "[min, max)" }, new() { Value = "maxInclusive", Label = "(min, max]" } }, Group = "Range" },
                new() { Key = "outputMode", Label = "Output Mode", Type = PropertyType.Select, DefaultValue = "both", Options = new() { new() { Value = "boolean", Label = "Boolean Only" }, new() { Value = "both", Label = "Boolean + Value" } }, Group = "Output" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "boolean-logic",
            Name = "Boolean Logic",
            ShortLabel = "Bool",
            Category = "Logic",
            Description = "Logical operations: AND, OR, XOR, NOT, NAND, NOR",
            Icon = "fa-solid fa-microchip",
            Color = "#9C27B0",
            InputCount = 2,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "and", Options = new() { new() { Value = "and", Label = "AND" }, new() { Value = "or", Label = "OR" }, new() { Value = "xor", Label = "XOR" }, new() { Value = "not", Label = "NOT" }, new() { Value = "nand", Label = "NAND" }, new() { Value = "nor", Label = "NOR" } }, Group = "Operation" }
            }
        });

        // === DATA TRANSFORM NODES ===
        Register(new NodePluginDefinition
        {
            Id = "type-convert",
            Name = "Type Convert",
            ShortLabel = "Convert",
            Category = "Data Transform",
            Description = "Converts values between number, string, and boolean types",
            Icon = "fa-solid fa-exchange-alt",
            Color = "#00BCD4",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "targetType", Label = "Target Type", Type = PropertyType.Select, DefaultValue = "string", Options = new() { new() { Value = "number", Label = "Number" }, new() { Value = "string", Label = "String" }, new() { Value = "boolean", Label = "Boolean" } }, Group = "Conversion" },
                new() { Key = "onError", Label = "On Error", Type = PropertyType.Select, DefaultValue = "null", Options = new() { new() { Value = "null", Label = "Output Null" }, new() { Value = "original", Label = "Keep Original" }, new() { Value = "default", Label = "Use Default" } }, Group = "Conversion" },
                new() { Key = "defaultValue", Label = "Default Value", Type = PropertyType.Text, DefaultValue = "", Group = "Conversion" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "string-ops",
            Name = "String Operations",
            ShortLabel = "String",
            Category = "Data Transform",
            Description = "String manipulation: case, trim, replace, split, and more",
            Icon = "fa-solid fa-font",
            Color = "#795548",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "uppercase", Options = new() { new() { Value = "uppercase", Label = "Uppercase" }, new() { Value = "lowercase", Label = "Lowercase" }, new() { Value = "trim", Label = "Trim" }, new() { Value = "length", Label = "Length" }, new() { Value = "contains", Label = "Contains" }, new() { Value = "replace", Label = "Replace" }, new() { Value = "substring", Label = "Substring" }, new() { Value = "split", Label = "Split" }, new() { Value = "startsWith", Label = "Starts With" }, new() { Value = "endsWith", Label = "Ends With" }, new() { Value = "reverse", Label = "Reverse" } }, Group = "Operation" },
                new() { Key = "searchText", Label = "Search Text", Type = PropertyType.Text, DefaultValue = "", Group = "Parameters" },
                new() { Key = "replaceWith", Label = "Replace With", Type = PropertyType.Text, DefaultValue = "", Group = "Parameters" },
                new() { Key = "startIndex", Label = "Start Index", Type = PropertyType.Integer, DefaultValue = "0", Group = "Parameters" },
                new() { Key = "endIndex", Label = "End Index", Type = PropertyType.Integer, DefaultValue = "-1", Group = "Parameters" },
                new() { Key = "delimiter", Label = "Delimiter", Type = PropertyType.Text, DefaultValue = ",", Group = "Parameters" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "array-ops",
            Name = "Array Operations",
            ShortLabel = "Array",
            Category = "Data Transform",
            Description = "Array operations: get, length, first, last, join, slice",
            Icon = "fa-solid fa-layer-group",
            Color = "#1976D2",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "length", Options = new() { new() { Value = "get-element", Label = "Get Element" }, new() { Value = "length", Label = "Length" }, new() { Value = "first", Label = "First" }, new() { Value = "last", Label = "Last" }, new() { Value = "join", Label = "Join" }, new() { Value = "slice", Label = "Slice" }, new() { Value = "includes", Label = "Includes" }, new() { Value = "index-of", Label = "Index Of" } }, Group = "Operation" },
                new() { Key = "index", Label = "Index", Type = PropertyType.Integer, DefaultValue = "0", Group = "Parameters" },
                new() { Key = "separator", Label = "Separator", Type = PropertyType.Text, DefaultValue = ",", Group = "Parameters" },
                new() { Key = "start", Label = "Start", Type = PropertyType.Integer, DefaultValue = "0", Group = "Parameters" },
                new() { Key = "end", Label = "End", Type = PropertyType.Integer, DefaultValue = "-1", Group = "Parameters" },
                new() { Key = "searchValue", Label = "Search Value", Type = PropertyType.Text, DefaultValue = "", Group = "Parameters" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "json-ops",
            Name = "JSON Operations",
            ShortLabel = "JSON",
            Category = "Data Transform",
            Description = "Parse, stringify, and access JSON properties",
            Icon = "fa-solid fa-code",
            Color = "#FF6F00",
            InputCount = 1,
            OutputCount = 1,
            Properties = new()
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "get-property", Options = new() { new() { Value = "parse", Label = "Parse" }, new() { Value = "stringify", Label = "Stringify" }, new() { Value = "get-property", Label = "Get Property" }, new() { Value = "has-property", Label = "Has Property" }, new() { Value = "keys", Label = "Keys" }, new() { Value = "values", Label = "Values" } }, Group = "Operation" },
                new() { Key = "path", Label = "Property Path", Type = PropertyType.Text, DefaultValue = "", HelpText = "Dot notation, e.g. user.address.city", Group = "Operation" }
            }
        });

        Register(new NodePluginDefinition
        {
            Id = "timeline",
            Name = "Timeline",
            ShortLabel = "Timeline",
            Category = "Data Transform",
            Description = "Buffers values over time or count for trend analysis",
            Icon = "fa-solid fa-chart-line",
            Color = "#0288D1",
            InputCount = 1,
            OutputCount = 2,
            Properties = new()
            {
                new() { Key = "maxEntries", Label = "Max Entries", Type = PropertyType.Integer, DefaultValue = "100", Min = 1, Max = 10000, Group = "Buffer" },
                new() { Key = "windowMs", Label = "Window (ms)", Type = PropertyType.Integer, DefaultValue = "0", HelpText = "0 = no time limit", Group = "Buffer" },
                new() { Key = "aggregation", Label = "Aggregation", Type = PropertyType.Select, DefaultValue = "last", Options = new() { new() { Value = "last", Label = "Last" }, new() { Value = "first", Label = "First" }, new() { Value = "avg", Label = "Average" }, new() { Value = "min", Label = "Minimum" }, new() { Value = "max", Label = "Maximum" }, new() { Value = "sum", Label = "Sum" }, new() { Value = "count", Label = "Count" }, new() { Value = "range", Label = "Range" } }, Group = "Aggregation" }
            }
        });

        // === UTILITY (EXTENDED) NODES ===
        Register(new NodePluginDefinition
        {
            Id = "comment",
            Name = "Comment",
            ShortLabel = "Note",
            Category = "Utility",
            Description = "Annotation node for documentation — does not execute",
            Icon = "fa-solid fa-comment",
            Color = "#FFC107",
            InputCount = 0,
            OutputCount = 0,
            Properties = new()
            {
                new() { Key = "text", Label = "Comment Text", Type = PropertyType.Text, DefaultValue = "", Group = "Content" },
                new() { Key = "fontSize", Label = "Font Size", Type = PropertyType.Select, DefaultValue = "medium", Options = new() { new() { Value = "small", Label = "Small" }, new() { Value = "medium", Label = "Medium" }, new() { Value = "large", Label = "Large" } }, Group = "Appearance" }
            }
        });
    }

    /// <summary>
    /// Registers a subflow as a reusable node plugin.
    /// </summary>
    public void RegisterSubflow(SubflowConfig subflow)
    {
        var plugin = new NodePluginDefinition
        {
            Id = subflow.Id,
            Name = subflow.Name,
            ShortLabel = subflow.Name.Length > 12 ? subflow.Name[..12] : subflow.Name,
            Category = "Subflows",
            Description = subflow.Description ?? $"Custom subflow with {subflow.Nodes.Count} nodes",
            Icon = subflow.Icon,
            Color = subflow.Color,
            InputCount = subflow.InputCount,
            OutputCount = subflow.OutputCount,
            Properties = new()
            {
                new() { Key = "_subflowId", Label = "Subflow ID", Type = PropertyType.Text, DefaultValue = subflow.Id, HelpText = "Internal subflow reference", Advanced = true }
            }
        };
        
        Register(plugin);
    }

    /// <summary>
    /// Unregisters a subflow node plugin.
    /// </summary>
    public void UnregisterSubflow(string subflowId)
    {
        lock (_lock)
        {
            if (_plugins.Remove(subflowId))
            {
                if (_byCategory.TryGetValue("Subflows", out var list))
                {
                    list.RemoveAll(p => p.Id == subflowId);
                    if (list.Count == 0)
                    {
                        _byCategory.Remove("Subflows");
                        _categoryOrder.Remove("Subflows");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a node type is a subflow.
    /// </summary>
    public bool IsSubflow(string nodeTypeId)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(nodeTypeId, out var plugin) && plugin.Category == "Subflows";
        }
    }
}
