namespace DataForeman.Drivers;

/// <summary>
/// Base configuration for protocol drivers.
/// </summary>
public abstract class DriverConfigBase
{
    /// <summary>
    /// Connection ID.
    /// </summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// Connection name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the connection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Host address or endpoint.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Reconnection interval in milliseconds.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum retry attempts for reconnection.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Configuration for OPC UA client driver.
/// </summary>
public class OpcUaDriverConfig : DriverConfigBase
{
    /// <summary>
    /// Driver type identifier.
    /// </summary>
    public const string DriverType = "OPCUA";

    /// <summary>
    /// OPC UA server endpoint URL.
    /// </summary>
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";

    /// <summary>
    /// Security policy (None, Basic256, Basic256Sha256).
    /// </summary>
    public string SecurityPolicy { get; set; } = "None";

    /// <summary>
    /// Security mode (None, Sign, SignAndEncrypt).
    /// </summary>
    public string SecurityMode { get; set; } = "None";

    /// <summary>
    /// Username for authentication (if required).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (if required).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Application name for the client.
    /// </summary>
    public string ApplicationName { get; set; } = "DataForeman.OpcUaClient";

    /// <summary>
    /// Session timeout in milliseconds.
    /// </summary>
    public int SessionTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Publishing interval for subscriptions in milliseconds.
    /// </summary>
    public int PublishingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Keep-alive interval in milliseconds.
    /// </summary>
    public int KeepAliveIntervalMs { get; set; } = 5000;

    public OpcUaDriverConfig()
    {
        Port = 4840;
    }
}

/// <summary>
/// Configuration for EtherNet/IP driver.
/// </summary>
public class EtherNetIpDriverConfig : DriverConfigBase
{
    /// <summary>
    /// Driver type identifier.
    /// </summary>
    public const string DriverType = "EIP";

    /// <summary>
    /// CPU slot for ControlLogix/CompactLogix.
    /// </summary>
    public int Slot { get; set; } = 0;

    /// <summary>
    /// Whether to use connected messaging.
    /// </summary>
    public bool UseConnectedMessaging { get; set; } = true;

    /// <summary>
    /// RPI (Requested Packet Interval) in milliseconds.
    /// </summary>
    public int RpiMs { get; set; } = 100;

    /// <summary>
    /// Program name prefix for program-scoped tags.
    /// </summary>
    public string? ProgramName { get; set; }

    public EtherNetIpDriverConfig()
    {
        Port = 44818;
    }
}

/// <summary>
/// Configuration for Siemens S7 driver.
/// </summary>
public class S7DriverConfig : DriverConfigBase
{
    /// <summary>
    /// Driver type identifier.
    /// </summary>
    public const string DriverType = "S7";

    /// <summary>
    /// PLC rack number.
    /// </summary>
    public int Rack { get; set; } = 0;

    /// <summary>
    /// PLC slot number.
    /// </summary>
    public int Slot { get; set; } = 2;

    /// <summary>
    /// PLC type (S7-300, S7-400, S7-1200, S7-1500).
    /// </summary>
    public string PlcType { get; set; } = "S7-1500";

    /// <summary>
    /// PDU size for communication.
    /// </summary>
    public int PduSize { get; set; } = 480;

    public S7DriverConfig()
    {
        Port = 102;
    }
}
