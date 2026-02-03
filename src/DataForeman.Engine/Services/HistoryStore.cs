using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// Stores historical tag values in SQLite for retrieval
/// </summary>
public class HistoryStore : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HistoryStore> _logger;
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private readonly ConcurrentQueue<HistoryPoint> _writeQueue = new();
    private Timer? _flushTimer;
    private readonly object _writeLock = new();

    public HistoryStore(IConfiguration configuration, ILogger<HistoryStore> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _dbPath = _configuration.GetValue<string>("DataPath") ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(_dbPath);
    }

    public void Initialize()
    {
        var connectionString = $"Data Source={Path.Combine(_dbPath, "history.db")}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // Create tables
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS tag_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                connection_id TEXT NOT NULL,
                tag_id TEXT NOT NULL,
                value REAL,
                quality INTEGER NOT NULL,
                timestamp INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_tag_history_lookup
                ON tag_history (connection_id, tag_id, timestamp DESC);
        ";
        cmd.ExecuteNonQuery();

        // Start background flush timer (every 500ms)
        _flushTimer = new Timer(_ => FlushQueue(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

        _logger.LogInformation("History store initialized at {Path}", _dbPath);
    }

    /// <summary>
    /// Queue a value for storage
    /// </summary>
    public void Store(string connectionId, string tagId, object? value, TagQuality quality, long timestamp)
    {
        _writeQueue.Enqueue(new HistoryPoint
        {
            ConnectionId = connectionId,
            TagId = tagId,
            Value = ConvertToDouble(value),
            Quality = quality,
            Timestamp = timestamp
        });
    }

    /// <summary>
    /// Retrieve historical data for a tag
    /// </summary>
    public List<TagDataPoint> Query(string connectionId, string tagId, long startTime, long endTime, int maxPoints = 1000)
    {
        if (_connection == null)
            return new List<TagDataPoint>();

        var results = new List<TagDataPoint>();

        lock (_writeLock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT value, quality, timestamp
                FROM tag_history
                WHERE connection_id = $connId AND tag_id = $tagId
                  AND timestamp >= $start AND timestamp <= $end
                ORDER BY timestamp ASC
                LIMIT $limit
            ";
            cmd.Parameters.AddWithValue("$connId", connectionId);
            cmd.Parameters.AddWithValue("$tagId", tagId);
            cmd.Parameters.AddWithValue("$start", startTime);
            cmd.Parameters.AddWithValue("$end", endTime);
            cmd.Parameters.AddWithValue("$limit", maxPoints);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new TagDataPoint
                {
                    Value = reader.IsDBNull(0) ? null : reader.GetDouble(0),
                    Quality = (TagQuality)reader.GetInt32(1),
                    Timestamp = reader.GetInt64(2)
                });
            }
        }

        return results;
    }

    private void FlushQueue()
    {
        if (_connection == null || _writeQueue.IsEmpty)
            return;

        lock (_writeLock)
        {
            using var transaction = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO tag_history (connection_id, tag_id, value, quality, timestamp)
                VALUES ($connId, $tagId, $value, $quality, $timestamp)
            ";

            var connIdParam = cmd.Parameters.Add("$connId", SqliteType.Text);
            var tagIdParam = cmd.Parameters.Add("$tagId", SqliteType.Text);
            var valueParam = cmd.Parameters.Add("$value", SqliteType.Real);
            var qualityParam = cmd.Parameters.Add("$quality", SqliteType.Integer);
            var timestampParam = cmd.Parameters.Add("$timestamp", SqliteType.Integer);

            var count = 0;
            while (_writeQueue.TryDequeue(out var point) && count < 1000)
            {
                connIdParam.Value = point.ConnectionId;
                tagIdParam.Value = point.TagId;
                valueParam.Value = point.Value.HasValue ? point.Value.Value : DBNull.Value;
                qualityParam.Value = (int)point.Quality;
                timestampParam.Value = point.Timestamp;
                cmd.ExecuteNonQuery();
                count++;
            }

            transaction.Commit();

            if (count > 0)
                _logger.LogDebug("Flushed {Count} history points", count);
        }
    }

    private static double? ConvertToDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        short s => s,
        bool b => b ? 1 : 0,
        _ => null
    };

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushQueue();
        _connection?.Dispose();
    }

    private record HistoryPoint
    {
        public required string ConnectionId { get; init; }
        public required string TagId { get; init; }
        public double? Value { get; init; }
        public TagQuality Quality { get; init; }
        public long Timestamp { get; init; }
    }
}
