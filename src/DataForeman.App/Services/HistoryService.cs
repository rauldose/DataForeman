using Microsoft.Data.Sqlite;

namespace DataForeman.App.Services;

/// <summary>
/// Service for querying historical tag data from the Engine's SQLite HistoryStore
/// </summary>
public class HistoryService
{
    private readonly ILogger<HistoryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public HistoryService(IConfiguration configuration, ILogger<HistoryService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Use the same database path as the Engine
        var dbPath = _configuration.GetValue<string>("Database:Path") 
            ?? Path.Combine(AppContext.BaseDirectory, "..", "DataForeman.Engine", "data", "history.db");
        
        // Also check for common locations
        if (!File.Exists(dbPath))
        {
            var altPath = Path.Combine(AppContext.BaseDirectory, "data", "history.db");
            if (File.Exists(altPath))
            {
                dbPath = altPath;
            }
        }
        
        _connectionString = $"Data Source={dbPath}";
        _logger.LogInformation("History database path: {DbPath}", dbPath);
    }

    /// <summary>
    /// Get historical data for a tag within a time range
    /// </summary>
    public async Task<List<HistoricalDataPoint>> GetHistoricalDataAsync(
        string tagId, 
        DateTime startTime, 
        DateTime endTime, 
        int maxPoints = 500)
    {
        var records = new List<HistoricalDataPoint>();

        try
        {
            // Check if database exists
            var dbPath = _connectionString.Replace("Data Source=", "");
            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("History database not found at {DbPath}", dbPath);
                return records;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Query by tag_id (the tagId includes connection info like "Simulator/Temperature")
            // The tag_id in the database is just the tag name, so we need to extract it
            var tagName = tagId.Contains("/") ? tagId.Split('/').Last() : tagId;
            
            var sql = @"
                SELECT tag_id, value, quality, timestamp 
                FROM tag_history 
                WHERE tag_id = @tagId 
                    AND timestamp >= @startTime 
                    AND timestamp <= @endTime 
                ORDER BY timestamp ASC
                LIMIT @limit";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@tagId", tagName);
            command.Parameters.AddWithValue("@startTime", startTime.ToString("O"));
            command.Parameters.AddWithValue("@endTime", endTime.ToString("O"));
            command.Parameters.AddWithValue("@limit", maxPoints);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var valueStr = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (double.TryParse(valueStr, out var value))
                {
                    records.Add(new HistoricalDataPoint
                    {
                        Timestamp = DateTime.Parse(reader.GetString(3)),
                        Value = value,
                        Quality = reader.GetInt32(2)
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} historical points for tag {TagId}", records.Count, tagId);
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Error querying history database for tag {TagId}", tagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying historical data for {TagId}", tagId);
        }

        return records;
    }
}

/// <summary>
/// Represents a single historical data point
/// </summary>
public class HistoricalDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public int Quality { get; set; } = 192; // Good quality by default
}
