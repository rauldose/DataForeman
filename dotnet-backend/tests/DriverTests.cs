using DataForeman.Drivers;

namespace DataForeman.API.Tests;

/// <summary>
/// Unit tests for protocol drivers.
/// </summary>
public class DriverTests
{
    [Fact]
    public void OpcUaDriverConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new OpcUaDriverConfig();

        // Assert
        Assert.Equal(OpcUaDriverConfig.DriverType, "OPCUA");
        Assert.Equal(4840, config.Port);
        Assert.Equal("opc.tcp://localhost:4840", config.EndpointUrl);
        Assert.Equal("None", config.SecurityPolicy);
        Assert.Equal("None", config.SecurityMode);
    }

    [Fact]
    public void EtherNetIpDriverConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new EtherNetIpDriverConfig();

        // Assert
        Assert.Equal(EtherNetIpDriverConfig.DriverType, "EIP");
        Assert.Equal(44818, config.Port);
        Assert.Equal(0, config.Slot);
        Assert.True(config.UseConnectedMessaging);
    }

    [Fact]
    public void S7DriverConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new S7DriverConfig();

        // Assert
        Assert.Equal(S7DriverConfig.DriverType, "S7");
        Assert.Equal(102, config.Port);
        Assert.Equal(0, config.Rack);
        Assert.Equal(2, config.Slot);
        Assert.Equal("S7-1500", config.PlcType);
    }

    [Fact]
    public void TagValue_DefaultsToGoodQuality()
    {
        // Arrange & Act
        var tagValue = new TagValue
        {
            TagPath = "TestTag",
            Value = 42.0,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(0, tagValue.Quality); // 0 = Good
        Assert.Null(tagValue.StatusMessage);
    }

    [Fact]
    public void BrowseResult_PropertiesWork()
    {
        // Arrange & Act
        var result = new BrowseResult
        {
            Path = "ns=2;s=Demo.Temperature",
            Name = "Temperature",
            HasChildren = false,
            DataType = "Double",
            IsReadable = true,
            IsWritable = false
        };

        // Assert
        Assert.Equal("ns=2;s=Demo.Temperature", result.Path);
        Assert.Equal("Temperature", result.Name);
        Assert.False(result.HasChildren);
        Assert.Equal("Double", result.DataType);
        Assert.True(result.IsReadable);
        Assert.False(result.IsWritable);
    }

    [Theory]
    [InlineData(DriverConnectionState.Disconnected)]
    [InlineData(DriverConnectionState.Connecting)]
    [InlineData(DriverConnectionState.Connected)]
    [InlineData(DriverConnectionState.Error)]
    public void DriverConnectionState_AllStatesExist(DriverConnectionState state)
    {
        // Assert - just verifying the enum values exist
        Assert.True(Enum.IsDefined(typeof(DriverConnectionState), state));
    }
}
