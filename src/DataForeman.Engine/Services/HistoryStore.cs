using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace DataForeman.Engine.Services;

/// <summary>
/// SQLite-based historical data storage with background flushing.
/// </summary>
public class HistoryStore : IAsyncDisposable
{
    private readonly ILogger<HistoryStore> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly ConcurrentQueue<HistoryRecord> _buffer = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    private Timer? _flushTimer;
    private const int MaxBufferSize = 1000;
    private const int FlushIntervalMs = 1000;
    private const int ConnectionTimeoutSeconds = 30;
    private const int CommandTimeoutSeconds = 60;

    private volatile bool _isDisposing;
    private long _droppedRecords;

    /// <summary>
    /// Number of records dropped due to buffer overflow or flush failures.
    /// </summary>
    public long DroppedRecords => Interlocked.Read(ref _droppedRecords);

    public HistoryStore(IConfiguration configuration, ILogger<HistoryStore> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var dbPath = _configuration.GetValue<string>("Database:Path") 
            ?? Path.Combine(AppContext.BaseDirectory, "data", "history.db");
        
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Initializes the database schema.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);

            // Use cancellation token for timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));
            await connection.OpenAsync(cts.Token);

            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS tag_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    connection_id TEXT NOT NULL,
                    tag_id TEXT NOT NULL,
                    value TEXT,
                    quality INTEGER NOT NULL,
                    timestamp TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_tag_history_connection_tag
                    ON tag_history(connection_id, tag_id);
                CREATE INDEX IF NOT EXISTS idx_tag_history_timestamp
                    ON tag_history(timestamp);
                CREATE INDEX IF NOT EXISTS idx_tag_history_tag_timestamp
                    ON tag_history(tag_id, timestamp);
            ";
            await command.ExecuteNonQueryAsync(cts.Token);

            _logger.LogInformation("History database initialized at {ConnectionString}", _connectionString);

            // Start background flush timer with safe callback
            _flushTimer = new Timer(
                _ => SafeFlushBuffer(),
                null,
                TimeSpan.FromMilliseconds(FlushIntervalMs),
                TimeSpan.FromMilliseconds(FlushIntervalMs));
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout initializing history database after {Seconds} seconds", ConnectionTimeoutSeconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing history database");
            throw;
        }
    }

    /// <summary>
    /// Safe wrapper for timer callback that observes exceptions.
    /// </summary>
    private void SafeFlushBuffer()
    {
        if (_isDisposing) return;

        _ = FlushBufferAsync().ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                _logger.LogError(t.Exception, "Unobserved exception in FlushBufferAsync");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Stores a tag value in the buffer for background flushing.
    /// </summary>
    public Task StoreValueAsync(string connectionId, string tagId, object? value, int quality, DateTime timestamp)
    {
        if (_isDisposing) return Task.CompletedTask;

        // Check buffer size before adding to prevent unbounded growth
        if (_buffer.Count >= MaxBufferSize * 2)
        {
            // Buffer is critically full - drop the record and log warning
            Interlocked.Increment(ref _droppedRecords);
            _logger.LogWarning(
                "History buffer overflow - dropping record for tag {TagId}. Total dropped: {DroppedCount}",
                tagId, _droppedRecords);
            return Task.CompletedTask;
        }

        _buffer.Enqueue(new HistoryRecord
        {
            ConnectionId = connectionId,
            TagId = tagId,
            Value = value?.ToString(),
            Quality = quality,
            Timestamp = timestamp
        });

        // Trigger immediate flush if buffer is getting full (uses safe fire-and-forget)
        if (_buffer.Count >= MaxBufferSize)
        {
            SafeFlushBuffer();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Queries historical data for a tag.
    /// </summary>
    public async Task<List<HistoryRecord>> QueryAsync(
        string connectionId,
        string tagId,
        DateTime startTime,
        DateTime endTime,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var records = new List<HistoryRecord>();

        try
        {
            await using var connection = new SqliteConnection(_connectionString);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

            await connection.OpenAsync(cts.Token);

            var sql = @"
                SELECT connection_id, tag_id, value, quality, timestamp
                FROM tag_history
                WHERE connection_id = @connectionId
                    AND tag_id = @tagId
                    AND timestamp >= @startTime
                    AND timestamp <= @endTime
                ORDER BY timestamp DESC";

            if (limit.HasValue)
            {
                sql += " LIMIT @limit";
            }

            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = sql;
            command.Parameters.AddWithValue("@connectionId", connectionId);
            command.Parameters.AddWithValue("@tagId", tagId);
            command.Parameters.AddWithValue("@startTime", startTime.ToString("O"));
            command.Parameters.AddWithValue("@endTime", endTime.ToString("O"));
            if (limit.HasValue)
            {
                command.Parameters.AddWithValue("@limit", limit.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cts.Token);
            while (await reader.ReadAsync(cts.Token))
            {
                records.Add(new HistoryRecord
                {
                    ConnectionId = reader.GetString(0),
                    TagId = reader.GetString(1),
                    Value = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Quality = reader.GetInt32(3),
                    Timestamp = DateTime.Parse(reader.GetString(4))
                });
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Query for tag {TagId} timed out after {Seconds} seconds", tagId, ConnectionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying history for tag {TagId}", tagId);
        }

        return records;
    }

    /// <summary>
    /// Gets the latest value for a tag.
    /// </summary>
    public async Task<HistoryRecord?> GetLatestAsync(string connectionId, string tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

            await connection.OpenAsync(cts.Token);

            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = @"
                SELECT connection_id, tag_id, value, quality, timestamp
                FROM tag_history
                WHERE connection_id = @connectionId AND tag_id = @tagId
                ORDER BY timestamp DESC
                LIMIT 1";
            command.Parameters.AddWithValue("@connectionId", connectionId);
            command.Parameters.AddWithValue("@tagId", tagId);

            await using var reader = await command.ExecuteReaderAsync(cts.Token);
            if (await reader.ReadAsync(cts.Token))
            {
                return new HistoryRecord
                {
                    ConnectionId = reader.GetString(0),
                    TagId = reader.GetString(1),
                    Value = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Quality = reader.GetInt32(3),
                    Timestamp = DateTime.Parse(reader.GetString(4))
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GetLatest for tag {TagId} timed out after {Seconds} seconds", tagId, ConnectionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest value for tag {TagId}", tagId);
        }

        return null;
    }

    /// <summary>
    /// Deletes history older than the specified retention period.
    /// </summary>
    public async Task CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - retentionPeriod;

            await using var connection = new SqliteConnection(_connectionString);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

            await connection.OpenAsync(cts.Token);

            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = "DELETE FROM tag_history WHERE timestamp < @cutoffTime";
            command.Parameters.AddWithValue("@cutoffTime", cutoffTime.ToString("O"));

            var deletedRows = await command.ExecuteNonQueryAsync(cts.Token);
            _logger.LogInformation("Cleaned up {DeletedRows} history records older than {CutoffTime}", deletedRows, cutoffTime);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cleanup operation timed out after {Seconds} seconds", ConnectionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up history");
        }
    }

    private async Task FlushBufferAsync()
    {
        if (_buffer.IsEmpty || _isDisposing) return;

        if (!await _flushLock.WaitAsync(0))
        {
            return; // Another flush is in progress
        }

        try
        {
            var recordsToFlush = new List<HistoryRecord>();
            while (_buffer.TryDequeue(out var record) && recordsToFlush.Count < MaxBufferSize)
            {
                recordsToFlush.Add(record);
            }

            if (recordsToFlush.Count == 0) return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cts.Token);

            await using var transaction = await connection.BeginTransactionAsync(cts.Token);

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction as SqliteTransaction;
                command.CommandTimeout = CommandTimeoutSeconds;
                command.CommandText = @"
                    INSERT INTO tag_history (connection_id, tag_id, value, quality, timestamp)
                    VALUES (@connectionId, @tagId, @value, @quality, @timestamp)";

                var connParam = command.Parameters.Add("@connectionId", SqliteType.Text);
                var tagParam = command.Parameters.Add("@tagId", SqliteType.Text);
                var valueParam = command.Parameters.Add("@value", SqliteType.Text);
                var qualityParam = command.Parameters.Add("@quality", SqliteType.Integer);
                var timestampParam = command.Parameters.Add("@timestamp", SqliteType.Text);

                foreach (var record in recordsToFlush)
                {
                    connParam.Value = record.ConnectionId;
                    tagParam.Value = record.TagId;
                    valueParam.Value = record.Value ?? (object)DBNull.Value;
                    qualityParam.Value = record.Quality;
                    timestampParam.Value = record.Timestamp.ToString("O");
                    await command.ExecuteNonQueryAsync(cts.Token);
                }

                await transaction.CommitAsync(cts.Token);
                _logger.LogDebug("Flushed {Count} history records to database", recordsToFlush.Count);
            }
            catch (OperationCanceledException)
            {
                // On timeout, try to rollback but don't block
                try { await transaction.RollbackAsync(); }
                catch { /* Ignore rollback errors */ }

                // Re-queue records that weren't persisted (up to a limit to avoid infinite growth)
                var requeued = 0;
                foreach (var record in recordsToFlush)
                {
                    if (_buffer.Count < MaxBufferSize * 2)
                    {
                        _buffer.Enqueue(record);
                        requeued++;
                    }
                    else
                    {
                        Interlocked.Increment(ref _droppedRecords);
                    }
                }
                _logger.LogWarning("Flush timed out, re-queued {Requeued} of {Total} records", requeued, recordsToFlush.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Flush buffer timed out after {Seconds} seconds", ConnectionTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing history buffer");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposing = true;

        // Dispose timer first to prevent new callbacks
        _flushTimer?.Dispose();
        _flushTimer = null;

        // Final flush with timeout
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await FlushBufferAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during final flush on dispose");
        }

        // Log if any records were dropped
        if (_droppedRecords > 0)
        {
            _logger.LogWarning("History store disposed with {DroppedCount} dropped records", _droppedRecords);
        }

        _flushLock.Dispose();
    }
}

/// <summary>
/// Represents a historical data record.
/// </summary>
public class HistoryRecord
{
    public string ConnectionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int Quality { get; set; }
    public DateTime Timestamp { get; set; }
}
