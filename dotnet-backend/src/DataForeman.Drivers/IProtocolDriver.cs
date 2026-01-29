namespace DataForeman.Drivers;

/// <summary>
/// Represents a tag value read from an industrial protocol.
/// </summary>
public class TagValue
{
    /// <summary>
    /// The tag path/address.
    /// </summary>
    public string TagPath { get; set; } = string.Empty;

    /// <summary>
    /// The value read from the device.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Timestamp when the value was read.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Quality code (0 = Good, 1+ = Bad/Uncertain).
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Status message if quality is not good.
    /// </summary>
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Subscription callback for tag value changes.
/// </summary>
public delegate void TagValueChangedHandler(string tagPath, TagValue value);

/// <summary>
/// Driver connection state.
/// </summary>
public enum DriverConnectionState
{
    /// <summary>
    /// Driver is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Driver is connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// Driver is connected.
    /// </summary>
    Connected,

    /// <summary>
    /// Driver connection failed.
    /// </summary>
    Error
}

/// <summary>
/// Base interface for all protocol drivers (OPC UA, EtherNet/IP, S7).
/// </summary>
public interface IProtocolDriver : IAsyncDisposable
{
    /// <summary>
    /// Unique driver type identifier.
    /// </summary>
    string DriverType { get; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    DriverConnectionState ConnectionState { get; }

    /// <summary>
    /// Connection ID for this driver instance.
    /// </summary>
    Guid ConnectionId { get; }

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event EventHandler<DriverConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when a subscribed tag value changes.
    /// </summary>
    event TagValueChangedHandler? TagValueChanged;

    /// <summary>
    /// Connect to the device.
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the device.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read a single tag value.
    /// </summary>
    Task<TagValue> ReadTagAsync(string tagPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read multiple tag values.
    /// </summary>
    Task<IEnumerable<TagValue>> ReadTagsAsync(IEnumerable<string> tagPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a value to a tag.
    /// </summary>
    Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to tag value changes.
    /// </summary>
    Task<bool> SubscribeAsync(string tagPath, int pollRateMs = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to multiple tags.
    /// </summary>
    Task<int> SubscribeAsync(IEnumerable<string> tagPaths, int pollRateMs = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from a tag.
    /// </summary>
    Task<bool> UnsubscribeAsync(string tagPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from all tags.
    /// </summary>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Browse available tags/nodes.
    /// </summary>
    Task<IEnumerable<BrowseResult>> BrowseAsync(string path = "", CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from browsing tags/nodes.
/// </summary>
public class BrowseResult
{
    /// <summary>
    /// Full path to the node.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this node has children (is a folder/container).
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Data type if this is a tag.
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// Whether this is readable.
    /// </summary>
    public bool IsReadable { get; set; }

    /// <summary>
    /// Whether this is writable.
    /// </summary>
    public bool IsWritable { get; set; }
}
