using System.Collections.Concurrent;
using System.Diagnostics;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// High-speed polling engine that can scan tags at sub-50ms rates.
/// Uses timer-based polling with prioritized poll groups.
/// </summary>
public class PollEngine : IDisposable
{
    private readonly ILogger<PollEngine> _logger;
    private readonly MqttPublisher _mqtt;
    private readonly HistoryStore _history;
    private readonly ConcurrentDictionary<string, ConnectionState> _connections = new();
    private readonly ConcurrentDictionary<int, PollGroup> _pollGroups = new();
    private readonly object _statsLock = new();

    private long _scanCount;
    private double _totalScanTimeMs;
    private bool _running;

    public long ScanCount => _scanCount;
    public double AverageScanTimeMs => _scanCount > 0 ? _totalScanTimeMs / _scanCount : 0;
    public int ActiveConnections => _connections.Count(c => c.Value.Connected);
    public int ActiveTags => _connections.Values.Sum(c => c.Tags.Count);

    public PollEngine(ILogger<PollEngine> logger, MqttPublisher mqtt, HistoryStore history)
    {
        _logger = logger;
        _mqtt = mqtt;
        _history = history;
    }

    /// <summary>
    /// Load configuration and start polling
    /// </summary>
    public void LoadConfig(ConnectionsFile config)
    {
        Stop();

        _connections.Clear();
        _pollGroups.Clear();

        foreach (var conn in config.Connections.Where(c => c.Enabled))
        {
            var state = new ConnectionState
            {
                Config = conn,
                Connected = false,
                Tags = new ConcurrentDictionary<string, TagState>()
            };

            // Initialize tags
            foreach (var tag in conn.Tags.Where(t => t.Enabled))
            {
                state.Tags[tag.Id] = new TagState
                {
                    Config = tag,
                    Value = null,
                    Quality = TagQuality.NotConnected,
                    Timestamp = 0
                };

                // Add to poll group
                var pollGroup = _pollGroups.GetOrAdd(tag.PollRateMs, rate => new PollGroup
                {
                    RateMs = rate,
                    Tags = new ConcurrentBag<(string ConnectionId, string TagId)>()
                });
                pollGroup.Tags.Add((conn.Id, tag.Id));
            }

            _connections[conn.Id] = state;
        }

        _logger.LogInformation("Loaded {Connections} connections with {Tags} tags in {Groups} poll groups",
            _connections.Count, ActiveTags, _pollGroups.Count);

        Start();
    }

    /// <summary>
    /// Start all poll group timers
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;

        // Connect to all devices
        foreach (var conn in _connections.Values)
        {
            _ = ConnectAsync(conn);
        }

        // Start poll groups
        foreach (var group in _pollGroups.Values)
        {
            StartPollGroup(group);
        }

        _logger.LogInformation("Poll engine started");
    }

    /// <summary>
    /// Stop all polling
    /// </summary>
    public void Stop()
    {
        _running = false;

        foreach (var group in _pollGroups.Values)
        {
            group.Timer?.Dispose();
            group.Timer = null;
        }

        foreach (var conn in _connections.Values)
        {
            DisconnectAsync(conn).Wait();
        }

        _logger.LogInformation("Poll engine stopped");
    }

    private void StartPollGroup(PollGroup group)
    {
        // Use high-resolution timer for sub-50ms rates
        var interval = TimeSpan.FromMilliseconds(group.RateMs);

        group.Timer = new Timer(
            _ => PollGroupCallback(group),
            null,
            interval,
            interval
        );

        _logger.LogDebug("Started poll group at {Rate}ms with {Count} tags",
            group.RateMs, group.Tags.Count);
    }

    private void PollGroupCallback(PollGroup group)
    {
        if (!_running) return;

        var sw = Stopwatch.StartNew();

        try
        {
            // Poll all tags in this group in parallel
            Parallel.ForEach(group.Tags, tag =>
            {
                PollTag(tag.ConnectionId, tag.TagId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in poll group {Rate}ms", group.RateMs);
        }

        sw.Stop();

        lock (_statsLock)
        {
            _scanCount++;
            _totalScanTimeMs += sw.Elapsed.TotalMilliseconds;
        }
    }

    private void PollTag(string connectionId, string tagId)
    {
        if (!_connections.TryGetValue(connectionId, out var conn))
            return;

        if (!conn.Tags.TryGetValue(tagId, out var tag))
            return;

        try
        {
            // Read value based on connection type
            var (value, quality) = ReadTagValue(conn, tag);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Update state
            tag.Value = value;
            tag.Quality = quality;
            tag.Timestamp = timestamp;

            // Publish via MQTT
            var msg = new TagValueMessage
            {
                ConnectionId = connectionId,
                TagId = tagId,
                Value = value,
                Quality = quality,
                Timestamp = timestamp
            };
            _mqtt.PublishTagValue(msg);

            // Store history if enabled
            if (tag.Config.LogHistory)
            {
                _history.Store(connectionId, tagId, value, quality, timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error polling tag {Connection}/{Tag}", connectionId, tagId);
            tag.Quality = TagQuality.Bad;
        }
    }

    private (object? Value, TagQuality Quality) ReadTagValue(ConnectionState conn, TagState tag)
    {
        if (!conn.Connected)
            return (null, TagQuality.NotConnected);

        return conn.Config.Type switch
        {
            ConnectionType.Simulator => ReadSimulatorValue(tag),
            ConnectionType.OpcUa => ReadOpcUaValue(conn, tag),
            ConnectionType.EtherNetIP => ReadEtherNetIPValue(conn, tag),
            ConnectionType.S7 => ReadS7Value(conn, tag),
            ConnectionType.ModbusTcp => ReadModbusValue(conn, tag),
            _ => (null, TagQuality.Bad)
        };
    }

    #region Simulator Driver

    private (object? Value, TagQuality Quality) ReadSimulatorValue(TagState tag)
    {
        var time = DateTime.UtcNow;
        var seconds = time.TimeOfDay.TotalSeconds;

        var value = tag.Config.DataType switch
        {
            DataType.Bool => (seconds % 10) < 5,
            DataType.Int16 => (short)(Math.Sin(seconds / 10) * 100),
            DataType.Int32 => (int)(Math.Sin(seconds / 10) * 1000),
            DataType.Float => (float)(Math.Sin(seconds / 10) * 50 + 50 + Random.Shared.NextDouble() * 2),
            DataType.Double => Math.Sin(seconds / 10) * 50 + 50 + Random.Shared.NextDouble() * 2,
            _ => (object?)null
        };

        // Apply scale and offset
        if (value is double d && tag.Config.ScaleFactor.HasValue)
            value = d * tag.Config.ScaleFactor.Value + (tag.Config.Offset ?? 0);

        return (value, TagQuality.Good);
    }

    #endregion

    #region OPC UA Driver (placeholder)

    private (object? Value, TagQuality Quality) ReadOpcUaValue(ConnectionState conn, TagState tag)
    {
        // TODO: Implement OPC UA read using node-opcua or similar
        _logger.LogWarning("OPC UA driver not implemented");
        return (null, TagQuality.Bad);
    }

    #endregion

    #region EtherNet/IP Driver (placeholder)

    private (object? Value, TagQuality Quality) ReadEtherNetIPValue(ConnectionState conn, TagState tag)
    {
        // TODO: Implement EtherNet/IP read
        _logger.LogWarning("EtherNet/IP driver not implemented");
        return (null, TagQuality.Bad);
    }

    #endregion

    #region Siemens S7 Driver (placeholder)

    private (object? Value, TagQuality Quality) ReadS7Value(ConnectionState conn, TagState tag)
    {
        // TODO: Implement S7 read using S7.Net or similar
        _logger.LogWarning("S7 driver not implemented");
        return (null, TagQuality.Bad);
    }

    #endregion

    #region Modbus TCP Driver (placeholder)

    private (object? Value, TagQuality Quality) ReadModbusValue(ConnectionState conn, TagState tag)
    {
        // TODO: Implement Modbus TCP read
        _logger.LogWarning("Modbus TCP driver not implemented");
        return (null, TagQuality.Bad);
    }

    #endregion

    #region Connection Management

    private async Task ConnectAsync(ConnectionState conn)
    {
        try
        {
            conn.Connected = conn.Config.Type switch
            {
                ConnectionType.Simulator => true,  // Simulator is always "connected"
                _ => false  // Other drivers need implementation
            };

            _logger.LogInformation("Connected to {Name} ({Type})", conn.Config.Name, conn.Config.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Name}", conn.Config.Name);
            conn.Connected = false;
        }

        await Task.CompletedTask;
    }

    private async Task DisconnectAsync(ConnectionState conn)
    {
        conn.Connected = false;
        await Task.CompletedTask;
    }

    #endregion

    public void Dispose()
    {
        Stop();
    }

    private class ConnectionState
    {
        public required ConnectionConfig Config { get; init; }
        public bool Connected { get; set; }
        public required ConcurrentDictionary<string, TagState> Tags { get; init; }
    }

    private class TagState
    {
        public required TagConfig Config { get; init; }
        public object? Value { get; set; }
        public TagQuality Quality { get; set; }
        public long Timestamp { get; set; }
    }

    private class PollGroup
    {
        public int RateMs { get; init; }
        public required ConcurrentBag<(string ConnectionId, string TagId)> Tags { get; init; }
        public Timer? Timer { get; set; }
    }
}
