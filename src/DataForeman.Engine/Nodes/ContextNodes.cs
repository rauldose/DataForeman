using System.Text.Json;
using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Nodes;

/// <summary>
/// Context Get node - reads a value from context (global, flow, or node scope).
/// Similar to Node-RED's change/function node context access.
/// </summary>
public class ContextGetNode : INodeRuntime
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "context-get",
        DisplayName = "Context Get",
        Category = "Context",
        Description = "Read a value from internal tag context (global, flow, or node scope)",
        Icon = "fa-download",
        Color = "#16a085",
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
                new ConfigProperty 
                { 
                    Name = "scope", 
                    Label = "Scope", 
                    Type = "select", 
                    Required = true,
                    Options = new[]
                    {
                        new SelectOption { Value = "global", Label = "Global" },
                        new SelectOption { Value = "flow", Label = "Flow" },
                        new SelectOption { Value = "node", Label = "Node" }
                    }
                },
                new ConfigProperty { Name = "key", Label = "Key", Type = "string", Required = true },
                new ConfigProperty { Name = "defaultValue", Label = "Default Value", Type = "string" }
            }
        }
    };

    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<ContextGetConfig>();
        if (string.IsNullOrEmpty(config?.Key))
        {
            context.Logger.Warn("ContextGetNode: No key specified");
            context.Emitter.Emit("output", context.Message);
            return ValueTask.CompletedTask;
        }

        if (context.ContextStore == null)
        {
            context.Logger.Warn("ContextGetNode: Context store not available");
            context.Emitter.Emit("output", context.Message);
            return ValueTask.CompletedTask;
        }

        object? value = config.Scope?.ToLowerInvariant() switch
        {
            "flow" => context.ContextStore.GetFlow(config.Key),
            "node" => context.ContextStore.GetNode(config.Key),
            _ => context.ContextStore.GetGlobal(config.Key) // Default to global
        };

        // Use default value if not found
        value ??= config.DefaultValue;

        var payload = new Dictionary<string, object?>
        {
            ["key"] = config.Key,
            ["scope"] = config.Scope ?? "global",
            ["value"] = value
        };

        var jsonPayload = JsonDocument.Parse(JsonSerializer.Serialize(payload)).RootElement.Clone();
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: jsonPayload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );

        context.Logger.Info($"Context get [{config.Scope ?? "global"}:{config.Key}] = {value}");
        context.Emitter.Emit("output", outputMessage);

        return ValueTask.CompletedTask;
    }

    private sealed class ContextGetConfig
    {
        public string? Scope { get; set; }
        public string? Key { get; set; }
        public string? DefaultValue { get; set; }
    }
}

/// <summary>
/// Context Set node - writes a value to context (global, flow, or node scope).
/// Similar to Node-RED's change/function node context access.
/// </summary>
public class ContextSetNode : INodeRuntime
{
    public static NodeDescriptor Descriptor => new()
    {
        Type = "context-set",
        DisplayName = "Context Set",
        Category = "Context",
        Description = "Write a value to internal tag context (global, flow, or node scope)",
        Icon = "fa-upload",
        Color = "#16a085",
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
                new ConfigProperty 
                { 
                    Name = "scope", 
                    Label = "Scope", 
                    Type = "select", 
                    Required = true,
                    Options = new[]
                    {
                        new SelectOption { Value = "global", Label = "Global" },
                        new SelectOption { Value = "flow", Label = "Flow" },
                        new SelectOption { Value = "node", Label = "Node" }
                    }
                },
                new ConfigProperty { Name = "key", Label = "Key", Type = "string", Required = true },
                new ConfigProperty 
                { 
                    Name = "valueSource", 
                    Label = "Value Source", 
                    Type = "select",
                    Options = new[]
                    {
                        new SelectOption { Value = "payload", Label = "Message Payload" },
                        new SelectOption { Value = "property", Label = "Payload Property" },
                        new SelectOption { Value = "static", Label = "Static Value" }
                    }
                },
                new ConfigProperty { Name = "property", Label = "Property Name", Type = "string" },
                new ConfigProperty { Name = "staticValue", Label = "Static Value", Type = "string" }
            }
        }
    };

    public ValueTask ExecuteAsync(NodeExecutionContext context, CancellationToken ct)
    {
        var config = context.GetConfig<ContextSetConfig>();
        if (string.IsNullOrEmpty(config?.Key))
        {
            context.Logger.Warn("ContextSetNode: No key specified");
            context.Emitter.Emit("output", context.Message);
            return ValueTask.CompletedTask;
        }

        if (context.ContextStore == null)
        {
            context.Logger.Warn("ContextSetNode: Context store not available");
            context.Emitter.Emit("output", context.Message);
            return ValueTask.CompletedTask;
        }

        // Determine the value to store
        object? value = config.ValueSource?.ToLowerInvariant() switch
        {
            "property" => ExtractProperty(context.Message.Payload, config.Property),
            "static" => config.StaticValue,
            _ => ExtractPayloadValue(context.Message.Payload) // Default: use payload
        };

        // Store based on scope
        switch (config.Scope?.ToLowerInvariant())
        {
            case "flow":
                context.ContextStore.SetFlow(config.Key, value);
                break;
            case "node":
                context.ContextStore.SetNode(config.Key, value);
                break;
            default: // Global
                context.ContextStore.SetGlobal(config.Key, value);
                break;
        }

        context.Logger.Info($"Context set [{config.Scope ?? "global"}:{config.Key}] = {value}");

        // Pass through the original message
        var outputMessage = context.Message.Derive(
            createdUtc: context.CurrentUtc,
            payload: context.Message.Payload,
            sourceNodeId: context.Node.Id,
            sourcePort: "output"
        );
        context.Emitter.Emit("output", outputMessage);

        return ValueTask.CompletedTask;
    }

    private static object? ExtractPayloadValue(JsonElement? payload)
    {
        if (payload == null) return null;
        
        // Try to get "value" property, or return the whole payload
        if (payload.Value.TryGetProperty("value", out var valueElement))
        {
            return GetJsonValue(valueElement);
        }
        
        return payload.Value.GetRawText();
    }

    private static object? ExtractProperty(JsonElement? payload, string? propertyName)
    {
        if (payload == null || string.IsNullOrEmpty(propertyName)) return null;
        
        if (payload.Value.TryGetProperty(propertyName, out var element))
        {
            return GetJsonValue(element);
        }
        
        return null;
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private sealed class ContextSetConfig
    {
        public string? Scope { get; set; }
        public string? Key { get; set; }
        public string? ValueSource { get; set; }
        public string? Property { get; set; }
        public string? StaticValue { get; set; }
    }
}
