namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Registry for flow node plugins. Provides metadata-driven node definitions
/// that enable extensible node types for the flow editor.
/// </summary>
public class NodePluginRegistry
{
    private readonly Dictionary<string, NodePluginDefinition> _plugins = new();
    private readonly Dictionary<string, List<NodePluginDefinition>> _byCategory = new();
    private readonly List<string> _categoryOrder = new(); // Tracks order categories were added
    private readonly object _lock = new();
    
    /// <summary>
    /// Gets all registered node plugins
    /// </summary>
    public IReadOnlyCollection<NodePluginDefinition> All
    {
        get
        {
            lock (_lock)
            {
                return _plugins.Values.ToList().AsReadOnly();
            }
        }
    }
    
    /// <summary>
    /// Gets all category names in the order they were registered (automatically derived from plugins)
    /// </summary>
    public IReadOnlyList<string> Categories
    {
        get
        {
            lock (_lock)
            {
                return _categoryOrder.AsReadOnly();
            }
        }
    }
    
    public NodePluginRegistry()
    {
        RegisterBuiltInPlugins();
    }
    
    /// <summary>
    /// Registers a node plugin definition (thread-safe)
    /// </summary>
    public void Register(NodePluginDefinition plugin)
    {
        lock (_lock)
        {
            _plugins[plugin.Id] = plugin;
            
            if (!_byCategory.ContainsKey(plugin.Category))
            {
                _byCategory[plugin.Category] = new List<NodePluginDefinition>();
                _categoryOrder.Add(plugin.Category); // Track category order
            }
            
            var existing = _byCategory[plugin.Category].FindIndex(p => p.Id == plugin.Id);
            if (existing >= 0)
                _byCategory[plugin.Category][existing] = plugin;
            else
                _byCategory[plugin.Category].Add(plugin);
        }
    }
    
    /// <summary>
    /// Gets a node plugin by ID
    /// </summary>
    public NodePluginDefinition? Get(string id)
    {
        // Handle instance IDs (e.g., "trigger-manual-instance-123")
        var baseId = id.Contains("-instance-") ? id.Split("-instance-")[0] : id;
        lock (_lock)
        {
            return _plugins.TryGetValue(baseId, out var plugin) ? plugin : null;
        }
    }
    
    /// <summary>
    /// Gets plugins for a specific category
    /// </summary>
    public IReadOnlyList<NodePluginDefinition> GetByCategory(string category)
    {
        lock (_lock)
        {
            return _byCategory.TryGetValue(category, out var plugins) 
                ? plugins.ToList().AsReadOnly() 
                : Array.Empty<NodePluginDefinition>();
        }
    }
    
    /// <summary>
    /// Registers all built-in node plugins
    /// </summary>
    private void RegisterBuiltInPlugins()
    {
        // === TRIGGER NODES ===
        Register(new NodePluginDefinition
        {
            Id = "trigger-manual",
            Name = "Manual Trigger",
            ShortLabel = "Manual",
            Category = "Triggers",
            Description = "Fires when manually activated or when the flow starts",
            Icon = "[M]",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "runOnDeploy", Label = "Run on Deploy", Type = PropertyType.Boolean, DefaultValue = "false", HelpText = "Fire automatically when the flow is deployed", Group = "Behavior" }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "trigger-schedule",
            Name = "Schedule Trigger",
            ShortLabel = "Schedule",
            Category = "Triggers",
            Description = "Fires on a time-based schedule",
            Icon = "[T]",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "interval", Label = "Interval (seconds)", Type = PropertyType.Integer, DefaultValue = "60", Min = 1, Max = 86400, Group = "Schedule" },
                new() { Key = "cron", Label = "Cron Expression", Type = PropertyType.Cron, Placeholder = "0 */5 * * * *", HelpText = "Optional: Use cron syntax for complex schedules", Group = "Schedule", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "trigger-tag",
            Name = "Tag Change Trigger",
            ShortLabel = "On Change",
            Category = "Triggers",
            Description = "Fires when a tag value changes",
            Icon = "[C]",
            Color = "#22c55e",
            InputCount = 0,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Placeholder = "PLC1.Tank1.Temperature", Required = true, Group = "Tag" },
                new() { Key = "triggerMode", Label = "Trigger Mode", Type = PropertyType.Select, DefaultValue = "Any Change", Options = new() { new() { Value = "Any Change", Label = "Any Change" }, new() { Value = "Rising Edge", Label = "Rising Edge" }, new() { Value = "Falling Edge", Label = "Falling Edge" } }, Group = "Tag" },
                new() { Key = "deadband", Label = "Deadband", Type = PropertyType.Decimal, DefaultValue = "0", Min = 0, Step = 0.1, HelpText = "Minimum change required to trigger", Group = "Tag", Advanced = true }
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
            Icon = "[IN]",
            Color = "#3b82f6",
            InputCount = 1,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Placeholder = "PLC1.Tank1.Temperature", Required = true, Group = "Tag" },
                new() { Key = "dataType", Label = "Data Type", Type = PropertyType.Select, DefaultValue = "Float", Options = new() { new() { Value = "Float", Label = "Float" }, new() { Value = "Integer", Label = "Integer" }, new() { Value = "Boolean", Label = "Boolean" }, new() { Value = "String", Label = "String" } }, Group = "Tag" },
                new() { Key = "defaultValue", Label = "Default Value", Type = PropertyType.Text, DefaultValue = "0", Placeholder = "0", HelpText = "Value to use if tag read fails", Group = "Error Handling", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "tag-output",
            Name = "Tag Output",
            ShortLabel = "Write Tag",
            Category = "Tags",
            Description = "Writes a value to a tag",
            Icon = "[OUT]",
            Color = "#f59e0b",
            InputCount = 1,
            OutputCount = 0,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Placeholder = "PLC1.Tank1.SetPoint", Required = true, Group = "Tag" },
                new() { Key = "dataType", Label = "Data Type", Type = PropertyType.Select, DefaultValue = "Float", Options = new() { new() { Value = "Float", Label = "Float" }, new() { Value = "Integer", Label = "Integer" }, new() { Value = "Boolean", Label = "Boolean" }, new() { Value = "String", Label = "String" } }, Group = "Tag" }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "tag-write",
            Name = "Set Value",
            ShortLabel = "Set Value",
            Category = "Tags",
            Description = "Sets a constant value to output",
            Icon = "[WR]",
            Color = "#f97316",
            InputCount = 1,
            OutputCount = 0,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "tagPath", Label = "Tag Path", Type = PropertyType.TagPath, Placeholder = "PLC1.Tank1.SetPoint", Required = true, Group = "Tag" },
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
            Icon = "[+]",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.ReadOnly, DefaultValue = "A + B", Group = "Operation" },
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" },
                new() { Key = "precision", Label = "Result Precision", Type = PropertyType.Integer, DefaultValue = "2", Min = 0, Max = 10, Group = "Output", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "math-subtract",
            Name = "Subtract",
            ShortLabel = "Subtract",
            Category = "Math",
            Description = "Subtracts second value from first",
            Icon = "[-]",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.ReadOnly, DefaultValue = "A - B", Group = "Operation" },
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" },
                new() { Key = "precision", Label = "Result Precision", Type = PropertyType.Integer, DefaultValue = "2", Min = 0, Max = 10, Group = "Output", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "math-multiply",
            Name = "Multiply",
            ShortLabel = "Multiply",
            Category = "Math",
            Description = "Multiplies two values",
            Icon = "[×]",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.ReadOnly, DefaultValue = "A × B", Group = "Operation" },
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "1", Step = 1, Group = "Operation" },
                new() { Key = "precision", Label = "Result Precision", Type = PropertyType.Integer, DefaultValue = "2", Min = 0, Max = 10, Group = "Output", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "math-divide",
            Name = "Divide",
            ShortLabel = "Divide",
            Category = "Math",
            Description = "Divides first value by second",
            Icon = "[÷]",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.ReadOnly, DefaultValue = "A ÷ B", Group = "Operation" },
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "1", Min = 0.001, Step = 1, HelpText = "Cannot be zero", Group = "Operation" },
                new() { Key = "precision", Label = "Result Precision", Type = PropertyType.Integer, DefaultValue = "2", Min = 0, Max = 10, Group = "Output", Advanced = true }
            }
        });
        
        // Generic math node for legacy compatibility (used in seed data)
        Register(new NodePluginDefinition
        {
            Id = "math",
            Name = "Math",
            ShortLabel = "Math",
            Category = "Math",
            Description = "Generic math operation node (legacy)",
            Icon = "[±]",
            Color = "#a855f7",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = "add", Options = new() { new() { Value = "add", Label = "Add" }, new() { Value = "subtract", Label = "Subtract" }, new() { Value = "multiply", Label = "Multiply" }, new() { Value = "divide", Label = "Divide" }, new() { Value = "average", Label = "Average" } }, Group = "Operation" },
                new() { Key = "operand", Label = "Operand B (if no input)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" }
            }
        });
        
        // === LOGIC NODES ===
        Register(new NodePluginDefinition
        {
            Id = "logic-if",
            Name = "Branch",
            ShortLabel = "Branch",
            Category = "Logic",
            Description = "Routes flow based on condition",
            Icon = "[?]",
            Color = "#6b7280",
            InputCount = 1,
            OutputCount = 2,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "condition", Label = "Condition", Type = PropertyType.Select, DefaultValue = "truthy", Options = new() { new() { Value = "truthy", Label = "If Truthy" }, new() { Value = "falsy", Label = "If Falsy" }, new() { Value = "equals", Label = "Equals Value" }, new() { Value = "greater", Label = "Greater Than" }, new() { Value = "less", Label = "Less Than" } }, Group = "Condition" },
                new() { Key = "threshold", Label = "Compare Value", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Condition" }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "compare-equal",
            Name = "Equal",
            ShortLabel = "Equal",
            Category = "Logic",
            Description = "Compares if values are equal",
            Icon = "[=]",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Comparison", Type = PropertyType.ReadOnly, DefaultValue = "A == B", Group = "Operation" },
                new() { Key = "threshold", Label = "Compare Value (if no input B)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" },
                new() { Key = "hysteresis", Label = "Hysteresis", Type = PropertyType.Decimal, DefaultValue = "0", Min = 0, Step = 0.1, Group = "Operation", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "compare-greater",
            Name = "Greater Than",
            ShortLabel = "Greater",
            Category = "Logic",
            Description = "Checks if first value is greater than second",
            Icon = "[>]",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Comparison", Type = PropertyType.ReadOnly, DefaultValue = "A > B", Group = "Operation" },
                new() { Key = "threshold", Label = "Threshold (if no input B)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" },
                new() { Key = "hysteresis", Label = "Hysteresis", Type = PropertyType.Decimal, DefaultValue = "0", Min = 0, Step = 0.1, Group = "Operation", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "compare-less",
            Name = "Less Than",
            ShortLabel = "Less",
            Category = "Logic",
            Description = "Checks if first value is less than second",
            Icon = "[<]",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Comparison", Type = PropertyType.ReadOnly, DefaultValue = "A < B", Group = "Operation" },
                new() { Key = "threshold", Label = "Threshold (if no input B)", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" },
                new() { Key = "hysteresis", Label = "Hysteresis", Type = PropertyType.Decimal, DefaultValue = "0", Min = 0, Step = 0.1, Group = "Operation", Advanced = true }
            }
        });
        
        // Generic compare node for legacy compatibility (used in seed data)
        Register(new NodePluginDefinition
        {
            Id = "compare",
            Name = "Compare",
            ShortLabel = "Compare",
            Category = "Logic",
            Description = "Generic comparison node (legacy)",
            Icon = "[?]",
            Color = "#6b7280",
            InputCount = 2,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "operation", Label = "Operation", Type = PropertyType.Select, DefaultValue = ">", Options = new() { new() { Value = ">", Label = "Greater Than" }, new() { Value = "<", Label = "Less Than" }, new() { Value = "==", Label = "Equal" }, new() { Value = ">=", Label = "Greater Or Equal" }, new() { Value = "<=", Label = "Less Or Equal" } }, Group = "Operation" },
                new() { Key = "threshold", Label = "Threshold", Type = PropertyType.Decimal, DefaultValue = "0", Step = 1, Group = "Operation" }
            }
        });
        
        // === SCRIPT NODES ===
        Register(new NodePluginDefinition
        {
            Id = "function",
            Name = "Function",
            ShortLabel = "Function",
            Category = "Script",
            Description = "Execute custom JavaScript code to transform data",
            Icon = "[ƒ]",
            Color = "#f59e0b",
            InputCount = 1,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "code", Label = "JavaScript Code", Type = PropertyType.Code, DefaultValue = "// Transform input data\nreturn input;", Placeholder = "// Your code here\nreturn input;", HelpText = "Write JavaScript to transform the input. Use 'input' to access incoming data.", Group = "Code" },
                new() { Key = "timeout", Label = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = "5000", Min = 100, Max = 30000, HelpText = "Maximum execution time", Group = "Execution", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "csharp",
            Name = "C# Script",
            ShortLabel = "C# Script",
            Category = "Script",
            Description = "Execute custom C# code to transform data",
            Icon = "[C#]",
            Color = "#9b4dca",
            InputCount = 1,
            OutputCount = 1,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "code", Label = "C# Code", Type = PropertyType.Code, DefaultValue = "// Transform input data\nreturn input;", Placeholder = "// Your code here\nreturn input;", HelpText = "Write C# to transform the input. Use 'input' to access incoming data.", Group = "Code" },
                new() { Key = "timeout", Label = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = "5000", Min = 100, Max = 30000, HelpText = "Maximum execution time", Group = "Execution", Advanced = true }
            }
        });
        
        // === OUTPUT NODES ===
        Register(new NodePluginDefinition
        {
            Id = "debug-log",
            Name = "Debug Log",
            ShortLabel = "Log",
            Category = "Output",
            Description = "Logs values for debugging",
            Icon = "[D]",
            Color = "#ef4444",
            InputCount = 1,
            OutputCount = 0,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "message", Label = "Message Template", Type = PropertyType.TextArea, DefaultValue = "Value: {{value}}", Placeholder = "Value: {{value}}", HelpText = "Use {{value}} to include the input value", Group = "Message" },
                new() { Key = "logLevel", Label = "Log Level", Type = PropertyType.Select, DefaultValue = "Info", Options = new() { new() { Value = "Debug", Label = "Debug" }, new() { Value = "Info", Label = "Info" }, new() { Value = "Warning", Label = "Warning" }, new() { Value = "Error", Label = "Error" } }, Group = "Message" }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "notification",
            Name = "Notification",
            ShortLabel = "Alert",
            Category = "Output",
            Description = "Sends notifications/alerts",
            Icon = "[!]",
            Color = "#ec4899",
            InputCount = 1,
            OutputCount = 0,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "message", Label = "Message Template", Type = PropertyType.TextArea, Placeholder = "Alert: {{value}} detected", HelpText = "Use {{value}} to include the input value", Group = "Message" },
                new() { Key = "severity", Label = "Severity", Type = PropertyType.Select, DefaultValue = "Info", Options = new() { new() { Value = "Info", Label = "Info" }, new() { Value = "Warning", Label = "Warning" }, new() { Value = "Critical", Label = "Critical" } }, Group = "Message" },
                new() { Key = "channel", Label = "Channel", Type = PropertyType.Select, DefaultValue = "App", Options = new() { new() { Value = "App", Label = "In-App" }, new() { Value = "Email", Label = "Email" }, new() { Value = "SMS", Label = "SMS" }, new() { Value = "Webhook", Label = "Webhook" } }, Group = "Delivery" },
                new() { Key = "rateLimit", Label = "Rate Limit (minutes)", Type = PropertyType.Integer, DefaultValue = "0", Min = 0, Max = 1440, HelpText = "Minimum minutes between alerts (0 = no limit)", Group = "Delivery", Advanced = true }
            }
        });
        
        Register(new NodePluginDefinition
        {
            Id = "database-write",
            Name = "Database Write",
            ShortLabel = "Store",
            Category = "Output",
            Description = "Writes data to database",
            Icon = "[DB]",
            Color = "#6366f1",
            InputCount = 1,
            OutputCount = 0,
            Properties = new List<NodePropertyDefinition>
            {
                new() { Key = "tableName", Label = "Table Name", Type = PropertyType.Text, DefaultValue = "flow_data", Placeholder = "flow_data", Group = "Database" },
                new() { Key = "columnMapping", Label = "Column Mapping", Type = PropertyType.Json, DefaultValue = "{\"value\": \"value\", \"timestamp\": \"created_at\"}", Placeholder = "{\"value\": \"value\"}", HelpText = "JSON mapping of input fields to columns", Group = "Database", Advanced = true },
                new() { Key = "batchSize", Label = "Batch Size", Type = PropertyType.Integer, DefaultValue = "1", Min = 1, Max = 1000, HelpText = "Number of records to batch before writing", Group = "Performance", Advanced = true }
            }
        });
    }
}
