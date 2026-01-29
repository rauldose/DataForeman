using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Simulator service that generates fake telemetry data for testing.
/// Provides real-time simulated values for tags without requiring real hardware connections.
/// </summary>
public class SimulatorService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SimulatorService> _logger;
    private readonly ConcurrentDictionary<int, SimulatedTag> _simulatedTags = new();
    private readonly ConcurrentDictionary<Guid, ConnectionSimulator> _connectionSimulators = new();
    private Timer? _updateTimer;
    private bool _isRunning;

    public event Action<int, object, int>? OnTagValueChanged;

    public SimulatorService(IServiceScopeFactory scopeFactory, ILogger<SimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts the simulator service.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _updateTimer = new Timer(UpdateSimulatedValues, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));
        _logger.LogInformation("Simulator service started");
    }

    /// <summary>
    /// Stops the simulator service.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _updateTimer?.Dispose();
        _updateTimer = null;
        _logger.LogInformation("Simulator service stopped");
    }

    /// <summary>
    /// Loads and starts simulating tags for a connection.
    /// </summary>
    public async Task LoadConnectionTagsAsync(Guid connectionId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataForemanDbContext>();

            var connection = await dbContext.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == connectionId);

            if (connection == null) return;

            var simulator = new ConnectionSimulator
            {
                ConnectionId = connectionId,
                ConnectionName = connection.Name,
                IsEnabled = connection.Enabled
            };

            foreach (var tag in connection.Tags.Where(t => t.IsSubscribed && !t.IsDeleted))
            {
                var simTag = new SimulatedTag
                {
                    TagId = tag.TagId,
                    TagPath = tag.TagPath,
                    DataType = tag.DataType ?? "Float",
                    ConnectionId = connectionId
                };
                InitializeSimulatedTag(simTag);
                _simulatedTags[tag.TagId] = simTag;
                simulator.TagIds.Add(tag.TagId);
            }

            _connectionSimulators[connectionId] = simulator;
            _logger.LogInformation("Loaded {TagCount} tags for connection {ConnectionName}", simulator.TagIds.Count, connection.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading connection tags for {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Unloads tags for a connection.
    /// </summary>
    public void UnloadConnection(Guid connectionId)
    {
        if (_connectionSimulators.TryRemove(connectionId, out var simulator))
        {
            foreach (var tagId in simulator.TagIds)
            {
                _simulatedTags.TryRemove(tagId, out _);
            }
            _logger.LogInformation("Unloaded connection {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// Gets the current simulated value for a tag.
    /// </summary>
    public (object? Value, int Quality, DateTime Timestamp) GetTagValue(int tagId)
    {
        if (_simulatedTags.TryGetValue(tagId, out var tag))
        {
            return (tag.CurrentValue, tag.Quality, tag.LastUpdated);
        }
        return (null, 192, DateTime.UtcNow); // 192 = Bad quality
    }

    /// <summary>
    /// Gets all current tag values.
    /// </summary>
    public Dictionary<int, (object? Value, int Quality, DateTime Timestamp)> GetAllTagValues()
    {
        return _simulatedTags.ToDictionary(
            kvp => kvp.Key,
            kvp => ((object?)kvp.Value.CurrentValue, kvp.Value.Quality, kvp.Value.LastUpdated)
        );
    }

    /// <summary>
    /// Gets simulated tags for a connection.
    /// </summary>
    public List<SimulatedTag> GetConnectionTags(Guid connectionId)
    {
        return _simulatedTags.Values
            .Where(t => t.ConnectionId == connectionId)
            .ToList();
    }

    private void InitializeSimulatedTag(SimulatedTag tag)
    {
        var random = new Random();

        // Set up simulation parameters based on tag path/type
        if (tag.TagPath.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
        {
            tag.SimulationType = SimulationType.Sine;
            tag.BaseValue = 25.0;
            tag.Amplitude = 10.0;
            tag.Period = 60; // 60 second cycle
            tag.NoiseLevel = 0.5;
        }
        else if (tag.TagPath.Contains("Pressure", StringComparison.OrdinalIgnoreCase))
        {
            tag.SimulationType = SimulationType.Ramp;
            tag.BaseValue = 100.0;
            tag.Amplitude = 50.0;
            tag.Period = 120;
            tag.NoiseLevel = 1.0;
        }
        else if (tag.TagPath.Contains("Level", StringComparison.OrdinalIgnoreCase))
        {
            tag.SimulationType = SimulationType.Triangle;
            tag.BaseValue = 50.0;
            tag.Amplitude = 30.0;
            tag.Period = 90;
            tag.NoiseLevel = 0.2;
        }
        else if (tag.TagPath.Contains("Flow", StringComparison.OrdinalIgnoreCase))
        {
            tag.SimulationType = SimulationType.Random;
            tag.BaseValue = 150.0;
            tag.Amplitude = 20.0;
            tag.NoiseLevel = 5.0;
        }
        else if (tag.TagPath.Contains("Speed", StringComparison.OrdinalIgnoreCase))
        {
            tag.SimulationType = SimulationType.Step;
            tag.BaseValue = 1500.0;
            tag.Amplitude = 500.0;
            tag.Period = 30;
            tag.NoiseLevel = 10.0;
        }
        else if (tag.TagPath.Contains("Status", StringComparison.OrdinalIgnoreCase) || tag.DataType == "Boolean")
        {
            tag.SimulationType = SimulationType.Boolean;
            tag.Period = 15;
        }
        else
        {
            // Default: sine wave with random parameters
            tag.SimulationType = SimulationType.Sine;
            tag.BaseValue = random.NextDouble() * 100;
            tag.Amplitude = random.NextDouble() * 20 + 5;
            tag.Period = random.Next(30, 180);
            tag.NoiseLevel = random.NextDouble() * 2;
        }

        // Initialize with a value
        UpdateTagValue(tag);
    }

    private void UpdateSimulatedValues(object? state)
    {
        if (!_isRunning) return;

        var now = DateTime.UtcNow;
        foreach (var tag in _simulatedTags.Values)
        {
            UpdateTagValue(tag);
            OnTagValueChanged?.Invoke(tag.TagId, tag.CurrentValue!, tag.Quality);
        }
    }

    private void UpdateTagValue(SimulatedTag tag)
    {
        var random = new Random();
        var now = DateTime.UtcNow;
        var elapsed = (now - tag.StartTime).TotalSeconds;

        object value;
        switch (tag.SimulationType)
        {
            case SimulationType.Sine:
                value = tag.BaseValue + tag.Amplitude * Math.Sin(2 * Math.PI * elapsed / tag.Period) + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Cosine:
                value = tag.BaseValue + tag.Amplitude * Math.Cos(2 * Math.PI * elapsed / tag.Period) + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Ramp:
                var rampPhase = (elapsed % tag.Period) / tag.Period;
                value = tag.BaseValue + tag.Amplitude * rampPhase + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Triangle:
                var triPhase = (elapsed % tag.Period) / tag.Period;
                var triValue = triPhase < 0.5 ? triPhase * 2 : 2 - triPhase * 2;
                value = tag.BaseValue + tag.Amplitude * triValue + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Step:
                var stepPhase = (int)(elapsed / tag.Period) % 4;
                value = tag.BaseValue + tag.Amplitude * stepPhase * 0.25 + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Random:
                value = tag.BaseValue + (random.NextDouble() - 0.5) * tag.Amplitude + (random.NextDouble() - 0.5) * tag.NoiseLevel;
                break;

            case SimulationType.Boolean:
                var boolPhase = (int)(elapsed / tag.Period) % 2;
                value = boolPhase == 0;
                break;

            default:
                value = tag.BaseValue;
                break;
        }

        // Round numeric values
        if (value is double d)
        {
            value = Math.Round(d, 2);
        }

        // Simulate occasional bad quality (1% chance)
        tag.Quality = random.NextDouble() < 0.01 ? 192 : 0;
        tag.CurrentValue = value;
        tag.LastUpdated = now;
    }

    public void Dispose()
    {
        Stop();
        _simulatedTags.Clear();
        _connectionSimulators.Clear();
    }
}

/// <summary>
/// Represents a simulated tag with its simulation parameters.
/// </summary>
public class SimulatedTag
{
    public int TagId { get; set; }
    public string TagPath { get; set; } = string.Empty;
    public string DataType { get; set; } = "Float";
    public Guid ConnectionId { get; set; }
    public SimulationType SimulationType { get; set; }
    public double BaseValue { get; set; }
    public double Amplitude { get; set; }
    public double Period { get; set; } = 60; // seconds
    public double NoiseLevel { get; set; }
    public object? CurrentValue { get; set; }
    public int Quality { get; set; } // 0 = Good
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of simulation patterns.
/// </summary>
public enum SimulationType
{
    Sine,
    Cosine,
    Ramp,
    Triangle,
    Step,
    Random,
    Boolean,
    Constant
}

/// <summary>
/// Represents a connection's simulation state.
/// </summary>
public class ConnectionSimulator
{
    public Guid ConnectionId { get; set; }
    public string ConnectionName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<int> TagIds { get; set; } = new();
}
