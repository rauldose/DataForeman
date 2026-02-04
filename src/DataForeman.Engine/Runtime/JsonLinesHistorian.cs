// DataForeman Platform - AI Agent Implementation Directives
// Section 7: HISTORIAN (GRAFANA-LIKE)
// Append-only, JSON Lines (.jsonl), no arrays of points, no full-file loading.

using DataForeman.Shared.Runtime;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace DataForeman.Engine.Runtime;

/// <summary>
/// JSON Lines historian implementation.
/// Append-only with downsampling queries.
/// </summary>
public sealed class JsonLinesHistorian : IHistorianWriter, IHistorianQuery, IDisposable
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonLinesHistorian> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, StreamWriter> _writers = new();

    public JsonLinesHistorian(string dataDirectory, ILogger<JsonLinesHistorian> logger)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;

        if (!Directory.Exists(dataDirectory))
            Directory.CreateDirectory(dataDirectory);
    }

    /// <summary>
    /// Writes a measurement to the historian.
    /// Append-only to a .jsonl file.
    /// </summary>
    public async ValueTask WriteAsync(HistorianMeasurement measurement, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var fileName = SanitizeFileName(measurement.Name);
            var filePath = Path.Combine(_dataDirectory, $"{fileName}.jsonl");

            if (!_writers.TryGetValue(fileName, out var writer))
            {
                writer = new StreamWriter(filePath, append: true);
                _writers[fileName] = writer;
            }

            var json = JsonSerializer.Serialize(new
            {
                t = measurement.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                v = measurement.Value,
                q = measurement.Quality,
                tags = measurement.Tags
            });

            await writer.WriteLineAsync(json);
            await writer.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Queries historical data with mandatory aggregation.
    /// </summary>
    public async ValueTask<HistorianQueryResult> QueryAsync(HistorianQueryRequest request, CancellationToken ct)
    {
        var fileName = SanitizeFileName(request.Name);
        var filePath = Path.Combine(_dataDirectory, $"{fileName}.jsonl");

        if (!File.Exists(filePath))
        {
            return new HistorianQueryResult
            {
                Name = request.Name,
                Points = Array.Empty<HistorianDataPoint>(),
                BucketDuration = TimeSpan.Zero,
                TotalRawPoints = 0
            };
        }

        // Read all points in time range (streaming, not loading entire file)
        var rawPoints = new List<(DateTime Timestamp, double Value)>();

        await foreach (var line in ReadLinesAsync(filePath, ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("t", out var tProp) && 
                    DateTime.TryParse(tProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    if (timestamp >= request.StartUtc && timestamp < request.EndUtc)
                    {
                        if (root.TryGetProperty("v", out var vProp) && vProp.ValueKind == JsonValueKind.Number)
                        {
                            rawPoints.Add((timestamp, vProp.GetDouble()));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        if (rawPoints.Count == 0)
        {
            return new HistorianQueryResult
            {
                Name = request.Name,
                Points = Array.Empty<HistorianDataPoint>(),
                BucketDuration = TimeSpan.Zero,
                TotalRawPoints = 0
            };
        }

        // Calculate bucket duration to fit within MaxPoints
        var timeRange = request.EndUtc - request.StartUtc;
        var bucketDuration = TimeSpan.FromTicks(Math.Max(timeRange.Ticks / request.MaxPoints, TimeSpan.TicksPerSecond));

        // Aggregate into buckets
        var buckets = new Dictionary<long, List<double>>();

        foreach (var (timestamp, value) in rawPoints)
        {
            var bucketKey = (timestamp.Ticks / bucketDuration.Ticks) * bucketDuration.Ticks;
            if (!buckets.ContainsKey(bucketKey))
                buckets[bucketKey] = new List<double>();
            buckets[bucketKey].Add(value);
        }

        // Create aggregated points
        var points = new List<HistorianDataPoint>();

        foreach (var kvp in buckets.OrderBy(b => b.Key))
        {
            var bucketTimestamp = new DateTime(kvp.Key, DateTimeKind.Utc);
            var values = kvp.Value;

            var aggregatedValue = request.Aggregation switch
            {
                AggregationFunction.Average => values.Average(),
                AggregationFunction.Min => values.Min(),
                AggregationFunction.Max => values.Max(),
                AggregationFunction.Sum => values.Sum(),
                AggregationFunction.Count => values.Count,
                AggregationFunction.First => values.First(),
                AggregationFunction.Last => values.Last(),
                _ => values.Average()
            };

            points.Add(new HistorianDataPoint
            {
                TimestampUtc = bucketTimestamp,
                Value = aggregatedValue,
                PointCount = values.Count,
                Min = values.Min(),
                Max = values.Max()
            });
        }

        return new HistorianQueryResult
        {
            Name = request.Name,
            Points = points,
            BucketDuration = bucketDuration,
            TotalRawPoints = rawPoints.Count
        };
    }

    /// <summary>
    /// Gets available measurement names.
    /// </summary>
    public ValueTask<IReadOnlyList<string>> GetMeasurementNamesAsync(CancellationToken ct)
    {
        var files = Directory.GetFiles(_dataDirectory, "*.jsonl");
        var names = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        return ValueTask.FromResult<IReadOnlyList<string>>(names);
    }

    private static string SanitizeFileName(string name)
    {
        // Replace invalid characters with underscores
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Replace('.', '_').Replace('/', '_');
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            yield return line;
        }
    }

    public void Dispose()
    {
        foreach (var writer in _writers.Values)
        {
            writer.Dispose();
        }
        _writers.Clear();
        _writeLock.Dispose();
    }
}

/// <summary>
/// Null historian implementation for testing.
/// </summary>
public sealed class NullHistorian : IHistorianWriter, IHistorianQuery
{
    public static NullHistorian Instance { get; } = new();

    public ValueTask WriteAsync(HistorianMeasurement measurement, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask<HistorianQueryResult> QueryAsync(HistorianQueryRequest request, CancellationToken ct)
    {
        return ValueTask.FromResult(new HistorianQueryResult
        {
            Name = request.Name,
            Points = Array.Empty<HistorianDataPoint>(),
            BucketDuration = TimeSpan.Zero,
            TotalRawPoints = 0
        });
    }

    public ValueTask<IReadOnlyList<string>> GetMeasurementNamesAsync(CancellationToken ct)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
