// DataForeman Platform - AI Agent Implementation Directives
// Section 7: HISTORIAN (GRAFANA-LIKE)
// Append-only, JSON Lines, no arrays of points, no full-file loading.
// All access must go through IHistorianWriter and IHistorianQuery.

namespace DataForeman.Shared.Runtime;

/// <summary>
/// Interface for historian queries (dashboards only, never nodes).
/// All queries must bucket by time and aggregate per bucket.
/// Never return raw unbounded data to UI.
/// </summary>
public interface IHistorianQuery
{
    /// <summary>
    /// Queries historical data with mandatory aggregation.
    /// Results are bucketed by time to limit returned points.
    /// </summary>
    ValueTask<HistorianQueryResult> QueryAsync(HistorianQueryRequest request, CancellationToken ct);

    /// <summary>
    /// Gets available measurement names.
    /// </summary>
    ValueTask<IReadOnlyList<string>> GetMeasurementNamesAsync(CancellationToken ct);
}

/// <summary>
/// Historian query request with mandatory aggregation.
/// </summary>
public sealed record HistorianQueryRequest
{
    /// <summary>Measurement name/tag path to query.</summary>
    public required string Name { get; init; }

    /// <summary>Start time (inclusive).</summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>End time (exclusive).</summary>
    public required DateTime EndUtc { get; init; }

    /// <summary>
    /// Maximum number of points to return.
    /// The query will automatically bucket the time range to return at most this many points.
    /// </summary>
    public int MaxPoints { get; init; } = 500;

    /// <summary>Aggregation function to apply per bucket.</summary>
    public AggregationFunction Aggregation { get; init; } = AggregationFunction.Average;

    /// <summary>Optional tag filters.</summary>
    public IReadOnlyDictionary<string, string>? TagFilters { get; init; }
}

/// <summary>
/// Aggregation functions for downsampling.
/// </summary>
public enum AggregationFunction
{
    /// <summary>Average of values in bucket.</summary>
    Average,
    /// <summary>Minimum value in bucket.</summary>
    Min,
    /// <summary>Maximum value in bucket.</summary>
    Max,
    /// <summary>Sum of values in bucket.</summary>
    Sum,
    /// <summary>Count of values in bucket.</summary>
    Count,
    /// <summary>First value in bucket.</summary>
    First,
    /// <summary>Last value in bucket.</summary>
    Last
}

/// <summary>
/// Historian query result.
/// </summary>
public sealed record HistorianQueryResult
{
    /// <summary>Measurement name.</summary>
    public required string Name { get; init; }

    /// <summary>Aggregated data points.</summary>
    public required IReadOnlyList<HistorianDataPoint> Points { get; init; }

    /// <summary>Bucket duration used.</summary>
    public required TimeSpan BucketDuration { get; init; }

    /// <summary>Total raw points in time range (before aggregation).</summary>
    public int TotalRawPoints { get; init; }
}

/// <summary>
/// Aggregated data point.
/// </summary>
public sealed record HistorianDataPoint
{
    /// <summary>Bucket timestamp (start of bucket).</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>Aggregated value.</summary>
    public required double Value { get; init; }

    /// <summary>Number of raw points in this bucket.</summary>
    public int PointCount { get; init; }

    /// <summary>Minimum value in bucket (for range display).</summary>
    public double? Min { get; init; }

    /// <summary>Maximum value in bucket (for range display).</summary>
    public double? Max { get; init; }
}
