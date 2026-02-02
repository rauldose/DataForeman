# C# Roslyn Script Executor Implementation

## Overview

The C# Roslyn Script Executor enables runtime compilation and execution of C# code within DataForeman flows, replacing JavaScript with type-safe, compiled C# scripts.

## Architecture

### Components

```
FlowExecutionEngine
    └─> CSharpScriptExecutor (NodeExecutorBase)
        ├─> Microsoft.CodeAnalysis.CSharp.Scripting (Roslyn)
        ├─> ScriptGlobals (input, tags, flow context)
        └─> ScriptOptions (assemblies, imports)
```

### Flow of Execution

1. **Node Configuration** - Flow designer defines C# code in node config
2. **Script Compilation** - Roslyn compiles C# code at runtime
3. **Globals Creation** - Executor creates globals object with context
4. **Execution** - Compiled script runs with 5-second timeout
5. **Result Return** - Script return value passed to downstream nodes

## Implementation Details

### CSharpScriptExecutor Class

**Location**: `dotnet-backend/src/DataForeman.FlowEngine/NodeExecutors.cs`

**Key Features**:
- Node type: `"csharp"`
- Inherits from `NodeExecutorBase`
- Async execution with cancellation token
- Comprehensive error handling
- Timeout protection (5 seconds)

**Script Options**:
```csharp
private static readonly ScriptOptions DefaultScriptOptions = ScriptOptions.Default
    .WithReferences(
        typeof(object).Assembly,              // System.Private.CoreLib
        typeof(System.Linq.Enumerable).Assembly,  // System.Linq
        typeof(System.Collections.Generic.List<>).Assembly,  // System.Collections
        typeof(System.Math).Assembly,         // System.Runtime
        typeof(System.DateTime).Assembly      // System.Runtime
    )
    .WithImports(
        "System",
        "System.Linq",
        "System.Collections.Generic",
        "System.Math"
    );
```

### ScriptGlobals Class

Provides context to executing scripts:

```csharp
public class ScriptGlobals
{
    public dynamic input { get; set; }              // Input from connected nodes
    public Dictionary<string, object?> tags { get; set; }  // Tag values
    public dynamic flow { get; set; }               // Flow context
}
```

**Usage in Scripts**:
```csharp
// Access input values
var temperature = input.temperature as double? ?? 0;
var pressure = input.pressure as double? ?? 0;

// Access tags (future enhancement)
var threshold = tags["maxTemp"] as double? ?? 100;

// Access flow context
var flowId = flow.id;
var executionId = flow.executionId;
```

## Usage Guide

### Basic Script Example

**Node Configuration**:
```json
{
  "id": "script_node_1",
  "type": "csharp",
  "label": "Calculate Sum",
  "config": {
    "code": "var x = 5; var y = 10; return new { sum = x + y };"
  }
}
```

**Result**: `{ sum = 15 }`

### Using Input Values

**Node Configuration**:
```json
{
  "id": "script_node_2",
  "type": "csharp",
  "label": "Process Temperature",
  "config": {
    "code": "var temp = input.temperature as double? ?? 0; return new { celsius = temp, fahrenheit = temp * 9 / 5 + 32 };"
  }
}
```

Assumes upstream node provides `temperature` value.

### Complex Logic Example

**Temperature Alert System** (from seeded flows):
```csharp
var highAlert = input.highTemp as bool? ?? false;
var lowAlert = input.lowTemp as bool? ?? false;
var avgTemp = input.average as double? ?? 0.0;

if (highAlert)
{
    return new { 
        alert = true, 
        level = "high", 
        message = "Temperature too high!", 
        value = avgTemp 
    };
}
else if (lowAlert)
{
    return new { 
        alert = true, 
        level = "low", 
        message = "Temperature too low!", 
        value = avgTemp 
    };
}
else
{
    return new { 
        alert = false, 
        level = "normal", 
        message = "Temperature normal", 
        value = avgTemp 
    };
}
```

### String Formatting Example

**Production Efficiency Calculator** (from seeded flows):
```csharp
var efficiency = input.value as double? ?? 0.0;
var percentage = Math.Min(100, Math.Max(0, efficiency * 10));

return new
{
    efficiency = efficiency.ToString("F2"),
    percentage = percentage.ToString("F1"),
    rating = percentage > 80 ? "Excellent" : percentage > 60 ? "Good" : "Poor"
};
```

## Error Handling

### Compilation Errors

**Example**:
```csharp
// Invalid code: missing semicolon
var x = 5
return x;
```

**Error Result**:
```
C# compilation error: (2,10): error CS1002: ; expected
```

### Runtime Errors

**Example**:
```csharp
var x = input.value as int? ?? 0;
return x / 0;  // Division by zero
```

**Error Result**:
```
C# script execution error: Attempted to divide by zero.
```

### Timeout Errors

**Example**:
```csharp
// Infinite loop
while (true) { }
```

**Error Result**:
```
C# script execution timed out (5 seconds)
```

## Type System

### Working with Dynamic Input

Input values are dynamic, so use type casting:

```csharp
// Safe casting with null-coalescing
var intValue = input.myInt as int? ?? 0;
var doubleValue = input.myDouble as double? ?? 0.0;
var stringValue = input.myString as string ?? "";
var boolValue = input.myBool as bool? ?? false;

// Check if value exists
if (input.myValue != null)
{
    // Use value
}
```

### Returning Values

Scripts can return:
- **Anonymous objects**: `return new { key = value };`
- **Built-in types**: `return 42;`, `return "hello";`, `return true;`
- **Collections**: `return new List<int> { 1, 2, 3 };`
- **Complex objects**: Any .NET type

**Best Practice**: Return anonymous objects for structured data:
```csharp
return new
{
    status = "success",
    result = calculatedValue,
    timestamp = DateTime.UtcNow
};
```

## Performance Considerations

### Compilation Overhead

- **First execution**: ~100-500ms (compilation + execution)
- **Subsequent executions**: ~100-500ms (compilation occurs each time)
- **Future optimization**: Implement script caching

### Memory Usage

- Each script execution creates a new compilation
- Compiled assemblies are garbage collected
- Consider memory limits for high-frequency flows

### Best Practices

1. **Keep scripts simple**: Complex logic should be in separate node types
2. **Avoid loops**: Timeout protection may terminate long-running scripts
3. **Use appropriate data types**: Minimize type conversions
4. **Return minimal data**: Large objects increase memory usage

## Security Considerations

### Current Limitations

⚠️ **Scripts run in same process** - Full .NET framework access  
⚠️ **No sandboxing** - Scripts can access file system, network  
⚠️ **Resource limits** - Only timeout protection (5 seconds)  

### Mitigation Strategies

1. **Timeout Protection**: 5-second limit prevents runaway scripts
2. **Error Handling**: Exceptions don't crash the flow engine
3. **User Permissions**: Only authorized users can create flows

### Future Enhancements

- [ ] AppDomain isolation
- [ ] Assembly whitelisting
- [ ] Resource quotas (CPU, memory)
- [ ] Code analysis (static checks)
- [ ] Script approval workflow

## Debugging Tips

### Enable Verbose Logging

Set log level to `Debug` to see script compilation details.

### Test Scripts Incrementally

Start with simple scripts and add complexity:

1. **Test return value**: `return "hello";`
2. **Test input access**: `return input;`
3. **Test type casting**: `var x = input.value as int? ?? 0; return x;`
4. **Add business logic**: Complex calculations, conditions

### Common Issues

**Issue**: `input.myValue is null`  
**Solution**: Check that upstream node provides the value

**Issue**: `InvalidCastException`  
**Solution**: Use safe casting with null-coalescing: `as Type? ?? default`

**Issue**: Compilation error on valid C#  
**Solution**: Check if required using statements are in ScriptOptions

## Integration with Flow Engine

### Registration

Executor is registered in `FlowExecutionEngine` constructor:

```csharp
RegisterExecutor(new CSharpScriptExecutor());
```

### Node Discovery

Flow designer queries available node types, sees `"csharp"` type.

### Execution Flow

1. Flow engine receives execution request
2. Parses flow definition (nodes + edges)
3. Executes nodes in topological order
4. When encountering `"csharp"` node, calls `CSharpScriptExecutor`
5. Executor compiles and runs script
6. Returns result to flow engine
7. Result available to downstream nodes

## Testing

### Unit Test Example

```csharp
[Fact]
public async Task CSharpScriptExecutor_SimpleScript_ReturnsCorrectValue()
{
    // Arrange
    var executor = new CSharpScriptExecutor();
    var node = new FlowNode
    {
        Id = "test",
        Type = "csharp",
        Config = new Dictionary<string, JsonElement>
        {
            ["code"] = JsonSerializer.SerializeToElement("return 5 + 3;")
        }
    };
    var context = new FlowExecutionContext
    {
        FlowId = Guid.NewGuid(),
        ExecutionId = Guid.NewGuid()
    };

    // Act
    var result = await executor.ExecuteAsync(node, context);

    // Assert
    Assert.True(result.Success);
    Assert.Equal(8, result.Output);
}
```

### Integration Test

See `docs/FLOW_EXAMPLES.md` for complete flow examples using C# scripts.

## Comparison: JavaScript vs C#

| Feature | JavaScript (Node.js) | C# (Roslyn) |
|---------|---------------------|-------------|
| **Execution** | Interpreted | Compiled |
| **Type Safety** | Dynamic | Static (with casting) |
| **Performance** | ~100ms | ~100-500ms (with compilation) |
| **Library Access** | Node.js modules | Full .NET ecosystem |
| **Error Detection** | Runtime | Compile-time + runtime |
| **IntelliSense** | Limited | Full support (future) |
| **Syntax** | JavaScript | C# |
| **Timeout** | 5 seconds | 5 seconds |
| **Security** | VM sandbox | Same process |

## Future Enhancements

### Short Term

- [ ] Script caching to avoid recompilation
- [ ] Enhanced error messages with line numbers
- [ ] Support for async/await in scripts
- [ ] More default imports (DateTime, Regex, etc.)

### Medium Term

- [ ] UI code editor with syntax highlighting
- [ ] IntelliSense/autocomplete in web editor
- [ ] Script debugging capabilities
- [ ] Unit testing framework for scripts

### Long Term

- [ ] Script library/package system
- [ ] Compiled script pre-deployment
- [ ] Advanced security (sandboxing)
- [ ] Performance profiling tools

## References

- [Roslyn Scripting API](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md)
- [Microsoft.CodeAnalysis.CSharp.Scripting NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting/)
- [C# Language Reference](https://learn.microsoft.com/en-us/dotnet/csharp/)

## Support

For issues or questions:
- Check `TROUBLESHOOTING.md` for common problems
- Review `FLOW_EXAMPLES.md` for usage examples
- Examine seeded flows in database for reference implementations
