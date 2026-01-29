namespace DataForeman.RedisStreams;

/// <summary>
/// Configuration options for Redis connection.
/// </summary>
public class RedisConnectionOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Whether Redis is enabled. If false, the application will work without Redis.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Redis connection string (e.g., "localhost:6379" or "redis:6379,password=secret").
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Application name for Redis client identification.
    /// </summary>
    public string ClientName { get; set; } = "DataForeman";

    /// <summary>
    /// Connect timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Enable automatic reconnection.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Number of connection retry attempts.
    /// </summary>
    public int ConnectRetry { get; set; } = 3;

    /// <summary>
    /// Default stream name for telemetry data.
    /// </summary>
    public string TelemetryStream { get; set; } = "df:telemetry:raw";

    /// <summary>
    /// Consumer group name for processing telemetry.
    /// </summary>
    public string ConsumerGroup { get; set; } = "df-processors";

    /// <summary>
    /// Maximum stream length before trimming (approximate).
    /// </summary>
    public int MaxStreamLength { get; set; } = 100000;

    /// <summary>
    /// Batch size for reading from streams.
    /// </summary>
    public int ReadBatchSize { get; set; } = 100;

    /// <summary>
    /// Block timeout in milliseconds when reading from streams.
    /// </summary>
    public int BlockTimeoutMs { get; set; } = 5000;
}
