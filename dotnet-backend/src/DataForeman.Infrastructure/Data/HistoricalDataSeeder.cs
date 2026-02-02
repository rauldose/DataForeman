using DataForeman.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataForeman.Infrastructure.Data;

/// <summary>
/// Seeds historical tag value data for demo/testing purposes
/// </summary>
public class HistoricalDataSeeder
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<HistoricalDataSeeder> _logger;
    private readonly Random _random = new();

    public HistoricalDataSeeder(DataForemanDbContext context, ILogger<HistoricalDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds historical data for all active tags
    /// </summary>
    public async Task SeedHistoricalDataAsync(int daysOfHistory = 7, int samplesPerHour = 60)
    {
        _logger.LogInformation("Starting historical data seeding for {Days} days with {Samples} samples/hour", daysOfHistory, samplesPerHour);

        var tags = await _context.TagMetadata
            .Where(t => t.IsSubscribed && !t.IsDeleted)
            .ToListAsync();

        if (!tags.Any())
        {
            _logger.LogWarning("No tags found for seeding");
            return;
        }

        var now = DateTime.UtcNow;
        var startTime = now.AddDays(-daysOfHistory);
        var totalSamples = daysOfHistory * 24 * samplesPerHour;
        var intervalMinutes = 60.0 / samplesPerHour;

        var allValues = new List<TagValue>();
        var batchSize = 10000;

        foreach (var tag in tags)
        {
            _logger.LogInformation("Generating data for tag {TagId}: {TagPath}", tag.TagId, tag.TagPath);
            
            var tagValues = GenerateTagValues(tag, startTime, totalSamples, intervalMinutes);
            allValues.AddRange(tagValues);

            // Batch insert to avoid memory issues
            if (allValues.Count >= batchSize)
            {
                await _context.TagValues.AddRangeAsync(allValues);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved batch of {Count} values", allValues.Count);
                allValues.Clear();
            }
        }

        // Save remaining values
        if (allValues.Any())
        {
            await _context.TagValues.AddRangeAsync(allValues);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Saved final batch of {Count} values", allValues.Count);
        }

        _logger.LogInformation("Historical data seeding completed");
    }

    private List<TagValue> GenerateTagValues(TagMetadata tag, DateTime startTime, int totalSamples, double intervalMinutes)
    {
        var values = new List<TagValue>();
        var currentTime = startTime;

        for (int i = 0; i < totalSamples; i++)
        {
            var value = GenerateRealisticValue(tag, currentTime, i);
            values.Add(value);
            currentTime = currentTime.AddMinutes(intervalMinutes);
        }

        return values;
    }

    private TagValue GenerateRealisticValue(TagMetadata tag, DateTime timestamp, int sampleIndex)
    {
        var value = new TagValue
        {
            TagId = tag.TagId,
            Timestamp = timestamp,
            Quality = 0 // Good quality
        };

        // Generate realistic data based on tag type
        if (tag.DataType == "Boolean")
        {
            value.BooleanValue = GenerateBooleanPattern(sampleIndex);
        }
        else
        {
            value.NumericValue = GenerateNumericValue(tag, timestamp, sampleIndex);
        }

        return value;
    }

    private bool GenerateBooleanPattern(int sampleIndex)
    {
        // Simulate on/off cycles (on for 80% of time, off for 20%)
        return (sampleIndex % 100) < 80;
    }

    private double GenerateNumericValue(TagMetadata tag, DateTime timestamp, int sampleIndex)
    {
        var hour = timestamp.Hour;
        var dayOfWeek = (int)timestamp.DayOfWeek;
        
        // Base value depends on tag type
        double baseValue = tag.TagPath switch
        {
            // Temperature tags: 20-80°C with daily cycle
            var p when p.Contains("Temperature") => 50 + 20 * Math.Sin(hour * Math.PI / 12) + GaussianNoise(2),
            
            // Pressure tags: 100-200 kPa with variations
            var p when p.Contains("Pressure") => 150 + 30 * Math.Sin(hour * Math.PI / 12) + GaussianNoise(5),
            
            // Level tags: 20-90% with slow drift
            var p when p.Contains("Level") => 55 + 25 * Math.Sin(sampleIndex * Math.PI / 500) + GaussianNoise(3),
            
            // Flow tags: 10-100 L/min with daily pattern
            var p when p.Contains("Flow") => 50 + 30 * Math.Sin(hour * Math.PI / 12) + GaussianNoise(4),
            
            // Motor Speed: 1200-1800 RPM
            var p when p.Contains("Speed") => 1500 + 200 * Math.Sin(hour * Math.PI / 8) + GaussianNoise(10),
            
            // Motor Current: 20-60A
            var p when p.Contains("Current") => 40 + 15 * Math.Sin(hour * Math.PI / 12) + GaussianNoise(2),
            
            // Motor Power: 30-80 kW
            var p when p.Contains("Power") => 55 + 20 * Math.Sin(hour * Math.PI / 12) + GaussianNoise(3),
            
            // Production Rate: 100-500 units
            var p when p.Contains("Production_Rate") => 300 + 150 * (dayOfWeek < 5 ? 1 : 0.5) + GaussianNoise(20),
            
            // Quality Index: 85-99%
            var p when p.Contains("Quality_Index") => 92 + 5 * Math.Sin(hour * Math.PI / 24) + GaussianNoise(1),
            
            // Efficiency: 70-95%
            var p when p.Contains("Efficiency") => 82 + 10 * Math.Sin(hour * Math.PI / 16) + GaussianNoise(2),
            
            // Alarm Count: 0-10
            var p when p.Contains("Alarm_Count") => Math.Max(0, 2 + 3 * Math.Sin(sampleIndex * Math.PI / 100) + GaussianNoise(1)),
            
            _ => 50 + GaussianNoise(10)
        };

        // Add occasional spikes/anomalies (1% chance)
        if (_random.NextDouble() < 0.01)
        {
            baseValue *= (1 + (_random.NextDouble() - 0.5) * 0.4);
        }

        return Math.Round(baseValue, 2);
    }

    /// <summary>
    /// Generate Gaussian (normal) distributed noise using Box-Muller transform
    /// </summary>
    private double GaussianNoise(double stdDev)
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return stdDev * randStdNormal;
    }

    /// <summary>
    /// Check if historical data already exists
    /// </summary>
    public async Task<bool> HasHistoricalDataAsync()
    {
        return await _context.TagValues.AnyAsync();
    }

    /// <summary>
    /// Clear all historical data (for re-seeding)
    /// </summary>
    public async Task ClearHistoricalDataAsync()
    {
        _logger.LogWarning("Clearing all historical tag value data");
        await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"TagValues\" CASCADE");
        _logger.LogInformation("Historical data cleared");
    }
}
