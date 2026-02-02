using DataForeman.FlowEngine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DataForeman.API.Tests;

/// <summary>
/// Unit tests for flow engine node executors.
/// </summary>
public class FlowEngineTests
{
    [Fact]
    public async Task MathAddExecutor_AddsNumbers()
    {
        // Arrange
        var executor = new MathAddExecutor();
        var node = new FlowNode
        {
            Id = "add1",
            Type = "math-add",
            Config = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["input_a"] = System.Text.Json.JsonSerializer.SerializeToElement("input1"),
                ["input_b"] = System.Text.Json.JsonSerializer.SerializeToElement("input2")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("input1", 5.0);
        context.SetNodeOutput("input2", 3.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8.0, result.Output);
    }

    [Fact]
    public async Task MathSubtractExecutor_SubtractsNumbers()
    {
        // Arrange
        var executor = new MathSubtractExecutor();
        var node = new FlowNode
        {
            Id = "sub1",
            Type = "math-subtract",
            Config = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["input_a"] = System.Text.Json.JsonSerializer.SerializeToElement("input1"),
                ["input_b"] = System.Text.Json.JsonSerializer.SerializeToElement("input2")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("input1", 10.0);
        context.SetNodeOutput("input2", 4.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(6.0, result.Output);
    }

    [Fact]
    public async Task MathDivideExecutor_DividesByZero_ReturnsFail()
    {
        // Arrange
        var executor = new MathDivideExecutor();
        var node = new FlowNode
        {
            Id = "div1",
            Type = "math-divide",
            Config = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["input_a"] = System.Text.Json.JsonSerializer.SerializeToElement("input1"),
                ["input_b"] = System.Text.Json.JsonSerializer.SerializeToElement("input2")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("input1", 10.0);
        context.SetNodeOutput("input2", 0.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Division by zero", result.Error);
    }

    [Fact]
    public async Task CompareGreaterExecutor_ComparesCorrectly()
    {
        // Arrange
        var executor = new CompareGreaterExecutor();
        var node = new FlowNode
        {
            Id = "cmp1",
            Type = "compare-greater",
            Config = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["input_a"] = System.Text.Json.JsonSerializer.SerializeToElement("input1"),
                ["input_b"] = System.Text.Json.JsonSerializer.SerializeToElement("input2")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("input1", 10.0);
        context.SetNodeOutput("input2", 5.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(true, result.Output);
    }

    [Fact]
    public async Task ManualTriggerExecutor_ReturnsTriggerInfo()
    {
        // Arrange
        var executor = new ManualTriggerExecutor();
        var node = new FlowNode
        {
            Id = "trigger1",
            Type = "trigger-manual"
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public void FlowDefinition_SerializesAndDeserializes()
    {
        // Arrange
        var flow = new FlowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Flow",
            ExecutionMode = "continuous",
            ScanRateMs = 1000,
            Nodes = new List<FlowNode>
            {
                new() { Id = "node1", Type = "trigger-manual", X = 100, Y = 100 },
                new() { Id = "node2", Type = "math-add", X = 300, Y = 100 }
            },
            Edges = new List<FlowEdge>
            {
                new() { Id = "edge1", Source = "node1", Target = "node2" }
            }
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(flow);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<FlowDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(flow.Id, deserialized.Id);
        Assert.Equal(flow.Name, deserialized.Name);
        Assert.Equal(2, deserialized.Nodes.Count);
        Assert.Single(deserialized.Edges);
    }

    #region CSharp Script Executor Tests

    [Fact]
    public async Task CSharpScriptExecutor_SimpleScript_ReturnsValue()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script1",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement("return 5 + 3;")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8, result.Output);
    }

    [Fact]
    public async Task CSharpScriptExecutor_MathOperations_ReturnsCorrectResult()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script2",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(
                    "var x = 10.0; var y = 5.0; return new { sum = x + y, product = x * y, division = x / y };"
                )
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task CSharpScriptExecutor_WithInputValues_AccessesInput()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script3",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(
                    "var temp = input.GetDouble(\"temperature\"); return new { celsius = temp, fahrenheit = temp * 9 / 5 + 32 };"
                ),
                ["input_temperature"] = JsonSerializer.SerializeToElement("temp_source")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("temp_source", 25.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        if (!result.Success)
        {
            throw new Exception($"Test failed with error: {result.Error}");
        }
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task CSharpScriptExecutor_ConditionalLogic_ReturnsCorrectBranch()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script4",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(
                    "var value = input.GetDouble(\"value\"); if (value > 75) { return new { alert = true, level = \"high\" }; } else { return new { alert = false, level = \"normal\" }; }"
                ),
                ["input_value"] = JsonSerializer.SerializeToElement("value_source")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };
        context.SetNodeOutput("value_source", 80.0);

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task CSharpScriptExecutor_CompilationError_ReturnsFail()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script5",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement("var x = 5 return x;") // Missing semicolon
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("compilation error", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CSharpScriptExecutor_RuntimeError_ReturnsFail()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script6",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement("var x = 10; var y = 0; return x / y;")
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("error", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CSharpScriptExecutor_NoCode_ReturnsFail()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script7",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>()
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("No C# code provided", result.Error);
    }

    [Fact]
    public async Task CSharpScriptExecutor_StringFormatting_ReturnsFormattedStrings()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script8",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(
                    "var efficiency = 5.45678; return new { formatted = efficiency.ToString(\"F2\"), percentage = (efficiency * 10).ToString(\"F1\") };"
                )
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task CSharpScriptExecutor_LinqOperations_ReturnsAggregatedData()
    {
        // Arrange
        var executor = new CSharpScriptExecutor();
        var node = new FlowNode
        {
            Id = "script9",
            Type = "csharp",
            Config = new Dictionary<string, JsonElement>
            {
                ["code"] = JsonSerializer.SerializeToElement(
                    "var numbers = new[] { 1, 2, 3, 4, 5 }; return new { sum = numbers.Sum(), average = numbers.Average(), max = numbers.Max() };"
                )
            }
        };

        var context = new FlowExecutionContext
        {
            FlowId = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow
        };

        // Act
        var result = await executor.ExecuteAsync(node, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task IntegrationTest_FlowWithCSharpScript_ExecutesSuccessfully()
    {
        // Arrange - Create a simple flow: Trigger -> Math Add -> C# Script
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<FlowExecutionEngine>>();
        var engine = new FlowExecutionEngine(logger, serviceProvider);

        var flow = new FlowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Test Flow with C# Script",
            ExecutionMode = "manual",
            Nodes = new List<FlowNode>
            {
                new()
                {
                    Id = "trigger1",
                    Type = "trigger-manual",
                    X = 100,
                    Y = 100
                },
                new()
                {
                    Id = "add1",
                    Type = "math-add",
                    X = 300,
                    Y = 100,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["input_a"] = JsonSerializer.SerializeToElement("trigger1"),
                        ["input_b"] = JsonSerializer.SerializeToElement("trigger1")
                    }
                },
                new()
                {
                    Id = "script1",
                    Type = "csharp",
                    X = 500,
                    Y = 100,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["code"] = JsonSerializer.SerializeToElement("var sum = input.GetDouble(\"value\"); return new { original = sum, doubled = sum * 2 };"),
                        ["input_value"] = JsonSerializer.SerializeToElement("add1")
                    }
                }
            },
            Edges = new List<FlowEdge>
            {
                new() { Id = "e1", Source = "trigger1", Target = "add1" },
                new() { Id = "e2", Source = "add1", Target = "script1" }
            }
        };

        // Act
        var result = await engine.ExecuteAsync(flow);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.NodeOutputs);
        Assert.Contains("script1", result.NodeOutputs.Keys);
    }

    [Fact]
    public async Task IntegrationTest_TemperatureAlertFlow_DetectsHighTemperature()
    {
        // Arrange - Simulate the Temperature Alert System flow
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<FlowExecutionEngine>>();
        var engine = new FlowExecutionEngine(logger, serviceProvider);

        var flow = new FlowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Temperature Alert Test",
            ExecutionMode = "manual",
            Nodes = new List<FlowNode>
            {
                new()
                {
                    Id = "temp_input",
                    Type = "trigger-manual",
                    X = 100,
                    Y = 100
                },
                new()
                {
                    Id = "compare_high",
                    Type = "compare-greater",
                    X = 300,
                    Y = 100,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["input_a"] = JsonSerializer.SerializeToElement("temp_input"),
                        ["input_b"] = JsonSerializer.SerializeToElement("temp_input")
                    }
                },
                new()
                {
                    Id = "alert_logic",
                    Type = "csharp",
                    X = 500,
                    Y = 100,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["code"] = JsonSerializer.SerializeToElement(
                            "var highAlert = input.GetBool(\"highTemp\"); " +
                            "var avgTemp = 80.0; " +
                            "if (highAlert) { return new { alert = true, level = \"high\", message = \"Temperature too high!\" }; } " +
                            "else { return new { alert = false, level = \"normal\", message = \"Temperature normal\" }; }"
                        ),
                        ["input_highTemp"] = JsonSerializer.SerializeToElement("compare_high")
                    }
                }
            },
            Edges = new List<FlowEdge>
            {
                new() { Id = "e1", Source = "temp_input", Target = "compare_high" },
                new() { Id = "e2", Source = "compare_high", Target = "alert_logic" }
            }
        };

        // Act
        var result = await engine.ExecuteAsync(flow);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("alert_logic", result.NodeOutputs.Keys);
    }

    [Fact]
    public async Task IntegrationTest_ProductionEfficiencyCalculator_CalculatesCorrectly()
    {
        // Arrange - Production Efficiency: Rate / Power
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<FlowExecutionEngine>>();
        var engine = new FlowExecutionEngine(logger, serviceProvider);

        var flow = new FlowDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Production Efficiency Test",
            ExecutionMode = "manual",
            Nodes = new List<FlowNode>
            {
                new()
                {
                    Id = "rate_input",
                    Type = "trigger-manual",
                    X = 100,
                    Y = 100
                },
                new()
                {
                    Id = "power_input",
                    Type = "trigger-manual",
                    X = 100,
                    Y = 200
                },
                new()
                {
                    Id = "efficiency",
                    Type = "math-divide",
                    X = 300,
                    Y = 150,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["input_a"] = JsonSerializer.SerializeToElement("rate_input"),
                        ["input_b"] = JsonSerializer.SerializeToElement("power_input")
                    }
                },
                new()
                {
                    Id = "format_result",
                    Type = "csharp",
                    X = 500,
                    Y = 150,
                    Config = new Dictionary<string, JsonElement>
                    {
                        ["code"] = JsonSerializer.SerializeToElement(
                            "var efficiency = input.GetDouble(\"value\"); " +
                            "var percentage = Math.Min(100, Math.Max(0, efficiency * 10)); " +
                            "return new { efficiency = efficiency.ToString(\"F2\"), percentage = percentage.ToString(\"F1\"), rating = percentage > 80 ? \"Excellent\" : percentage > 60 ? \"Good\" : \"Poor\" };"
                        ),
                        ["input_value"] = JsonSerializer.SerializeToElement("efficiency")
                    }
                }
            },
            Edges = new List<FlowEdge>
            {
                new() { Id = "e1", Source = "rate_input", Target = "efficiency" },
                new() { Id = "e2", Source = "power_input", Target = "efficiency" },
                new() { Id = "e3", Source = "efficiency", Target = "format_result" }
            }
        };

        // Act
        var result = await engine.ExecuteAsync(flow);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("format_result", result.NodeOutputs.Keys);
    }

    #endregion
}

