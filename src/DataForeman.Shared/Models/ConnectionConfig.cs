namespace DataForeman.Shared.Models;

/// <summary>
/// Configuration for a device connection
/// </summary>
public class ConnectionConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public ConnectionType Type { get; set; } = ConnectionType.Simulator;
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Options { get; set; } = new();
    public List<TagConfig> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ConnectionType
{
    Simulator,
    OpcUa,
    EtherNetIP,
    S7,
    ModbusTcp,
    Mqtt
}

/// <summary>
/// Configuration for a tag (data point)
/// </summary>
public class TagConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";  // PLC address, OPC node, etc.
    public DataType DataType { get; set; } = DataType.Float;
    public int PollRateMs { get; set; } = 1000;  // Polling interval
    public bool Enabled { get; set; } = true;
    public bool LogHistory { get; set; } = true;
    public double? ScaleFactor { get; set; }
    public double? Offset { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

public enum DataType
{
    Bool,
    Int16,
    Int32,
    Int64,
    Float,
    Double,
    String
}

/// <summary>
/// Container for all connection configurations
/// </summary>
public class ConnectionsFile
{
    public string Version { get; set; } = "1.0";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public List<ConnectionConfig> Connections { get; set; } = new();
}
