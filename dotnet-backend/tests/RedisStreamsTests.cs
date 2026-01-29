using DataForeman.RedisStreams;

namespace DataForeman.API.Tests;

/// <summary>
/// Unit tests for Redis Streams messages.
/// </summary>
public class RedisStreamsTests
{
    [Fact]
    public void TelemetryMessage_SerializesToJson()
    {
        // Arrange
        var message = new TelemetryMessage
        {
            ConnectionId = Guid.NewGuid(),
            TagId = 123,
            Timestamp = DateTime.UtcNow,
            Value = 42.5,
            Quality = 0
        };

        // Act
        var json = message.ToJson();
        var deserialized = TelemetryMessage.FromJson(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(message.ConnectionId, deserialized.ConnectionId);
        Assert.Equal(message.TagId, deserialized.TagId);
        Assert.Equal(message.Quality, deserialized.Quality);
    }

    [Fact]
    public void FlowExecutionMessage_SerializesToJson()
    {
        // Arrange
        var message = new FlowExecutionMessage
        {
            FlowId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            TriggerNodeId = "trigger1",
            Parameters = "{\"param1\": \"value1\"}",
            TriggeredAt = DateTime.UtcNow
        };

        // Act
        var json = message.ToJson();
        var deserialized = FlowExecutionMessage.FromJson(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(message.FlowId, deserialized.FlowId);
        Assert.Equal(message.SessionId, deserialized.SessionId);
        Assert.Equal(message.TriggerNodeId, deserialized.TriggerNodeId);
        Assert.Equal(message.Parameters, deserialized.Parameters);
    }

    [Fact]
    public void RedisConnectionOptions_HasValidDefaults()
    {
        // Arrange & Act
        var options = new RedisConnectionOptions();

        // Assert
        Assert.Equal("localhost:6379", options.ConnectionString);
        Assert.Equal("DataForeman", options.ClientName);
        Assert.Equal(5000, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(3, options.ConnectRetry);
        Assert.Equal("df:telemetry:raw", options.TelemetryStream);
        Assert.Equal("df-processors", options.ConsumerGroup);
        Assert.Equal(100000, options.MaxStreamLength);
        Assert.Equal(100, options.ReadBatchSize);
        Assert.Equal(5000, options.BlockTimeoutMs);
    }

    [Fact]
    public void StreamEntry_PropertiesWork()
    {
        // Arrange & Act
        var entry = new StreamEntry
        {
            MessageId = "1234567890-0",
            StreamName = "df:telemetry:raw",
            Data = new Dictionary<string, string>
            {
                ["connection_id"] = Guid.NewGuid().ToString(),
                ["tag_id"] = "123",
                ["v"] = "42.5"
            }
        };

        // Assert
        Assert.Equal("1234567890-0", entry.MessageId);
        Assert.Equal("df:telemetry:raw", entry.StreamName);
        Assert.Equal(3, entry.Data.Count);
    }

    [Theory]
    [InlineData(42.5)]
    [InlineData("string value")]
    [InlineData(true)]
    [InlineData(null)]
    public void TelemetryMessage_HandlesVariousValueTypes(object? value)
    {
        // Arrange
        var message = new TelemetryMessage
        {
            ConnectionId = Guid.NewGuid(),
            TagId = 1,
            Timestamp = DateTime.UtcNow,
            Value = value,
            Quality = 0
        };

        // Act
        var json = message.ToJson();

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
    }
}
