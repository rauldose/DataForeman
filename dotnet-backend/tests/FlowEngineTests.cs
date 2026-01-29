using DataForeman.FlowEngine;

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
}
