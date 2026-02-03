using DataForeman.Shared.Models;

namespace DataForeman.Engine.Drivers;

/// <summary>
/// Interface for data collection drivers.
/// </summary>
public interface IDriver : IAsyncDisposable
{
    string DriverType { get; }
    bool IsConnected { get; }
    Task ConnectAsync(ConnectionConfig connection);
    Task DisconnectAsync();
    Task<Dictionary<string, TagValue>> ReadTagsAsync(IEnumerable<TagConfig> tags);
    Task WriteTagAsync(TagConfig tag, object value);
}

/// <summary>
/// Represents a tag value with quality information.
/// </summary>
public class TagValue
{
    public string TagId { get; set; } = string.Empty;
    public object? Value { get; set; }
    public int Quality { get; set; } = 0; // 0 = Good
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Simulator driver for testing without hardware.
/// </summary>
public class SimulatorDriver : IDriver
{
    private readonly ILogger<SimulatorDriver> _logger;
    private ConnectionConfig? _connection;
    private readonly Dictionary<string, SimulatorTagState> _tagStates = new();
    private readonly Random _random = new();
    private bool _isConnected;

    public string DriverType => "Simulator";
    public bool IsConnected => _isConnected;

    public SimulatorDriver(ILogger<SimulatorDriver> logger)
    {
        _logger = logger;
    }

    public Task ConnectAsync(ConnectionConfig connection)
    {
        _connection = connection;
        _tagStates.Clear();

        foreach (var tag in connection.Tags.Where(t => t.Enabled))
        {
            _tagStates[tag.Id] = new SimulatorTagState
            {
                TagId = tag.Id,
                Settings = tag.Simulator ?? GetDefaultSettings(tag),
                DataType = tag.DataType,
                StartTime = DateTime.UtcNow
            };
        }

        _isConnected = true;
        _logger.LogInformation("Simulator driver connected: {ConnectionName} with {TagCount} tags",
            connection.Name, _tagStates.Count);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _tagStates.Clear();
        _logger.LogInformation("Simulator driver disconnected: {ConnectionName}", _connection?.Name);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, TagValue>> ReadTagsAsync(IEnumerable<TagConfig> tags)
    {
        var results = new Dictionary<string, TagValue>();

        foreach (var tag in tags)
        {
            if (!_tagStates.TryGetValue(tag.Id, out var state))
            {
                state = new SimulatorTagState
                {
                    TagId = tag.Id,
                    Settings = tag.Simulator ?? GetDefaultSettings(tag),
                    DataType = tag.DataType,
                    StartTime = DateTime.UtcNow
                };
                _tagStates[tag.Id] = state;
            }

            var value = GenerateValue(state);
            results[tag.Id] = new TagValue
            {
                TagId = tag.Id,
                Value = value,
                Quality = _random.NextDouble() < 0.01 ? 192 : 0, // 1% bad quality
                Timestamp = DateTime.UtcNow
            };
        }

        return Task.FromResult(results);
    }

    public Task WriteTagAsync(TagConfig tag, object value)
    {
        // Simulator doesn't support writes, but log for debugging
        _logger.LogDebug("Simulator write: {TagId} = {Value}", tag.Id, value);
        return Task.CompletedTask;
    }

    private object GenerateValue(SimulatorTagState state)
    {
        var elapsed = (DateTime.UtcNow - state.StartTime).TotalSeconds;
        var settings = state.Settings;

        double numericValue = settings.WaveType.ToLowerInvariant() switch
        {
            "sine" => settings.BaseValue + settings.Amplitude * Math.Sin(2 * Math.PI * elapsed / settings.PeriodSeconds) +
                      (_random.NextDouble() - 0.5) * settings.NoiseLevel,
            
            "cosine" => settings.BaseValue + settings.Amplitude * Math.Cos(2 * Math.PI * elapsed / settings.PeriodSeconds) +
                        (_random.NextDouble() - 0.5) * settings.NoiseLevel,
            
            "ramp" => settings.BaseValue + settings.Amplitude * (elapsed % settings.PeriodSeconds / settings.PeriodSeconds) +
                      (_random.NextDouble() - 0.5) * settings.NoiseLevel,
            
            "triangle" => GenerateTriangleValue(elapsed, settings),
            
            "step" => GenerateStepValue(elapsed, settings),
            
            "random" => settings.BaseValue + (_random.NextDouble() - 0.5) * settings.Amplitude +
                        (_random.NextDouble() - 0.5) * settings.NoiseLevel,
            
            "constant" => settings.BaseValue,
            
            "boolean" => (int)(elapsed / settings.PeriodSeconds) % 2,
            
            _ => settings.BaseValue
        };

        // Convert to appropriate type
        return state.DataType.ToLowerInvariant() switch
        {
            "boolean" or "bool" => numericValue > 0.5,
            "int16" => (short)Math.Round(numericValue),
            "int32" or "int" => (int)Math.Round(numericValue),
            "int64" or "long" => (long)Math.Round(numericValue),
            "double" => numericValue,
            "string" => Math.Round(numericValue, 2).ToString(),
            _ => Math.Round(numericValue, 2)
        };
    }

    private double GenerateTriangleValue(double elapsed, SimulatorSettings settings)
    {
        var phase = (elapsed % settings.PeriodSeconds) / settings.PeriodSeconds;
        var triValue = phase < 0.5 ? phase * 2 : 2 - phase * 2;
        return settings.BaseValue + settings.Amplitude * triValue +
               (_random.NextDouble() - 0.5) * settings.NoiseLevel;
    }

    private double GenerateStepValue(double elapsed, SimulatorSettings settings)
    {
        var stepPhase = (int)(elapsed / settings.PeriodSeconds) % 4;
        return settings.BaseValue + settings.Amplitude * stepPhase * 0.25 +
               (_random.NextDouble() - 0.5) * settings.NoiseLevel;
    }

    private SimulatorSettings GetDefaultSettings(TagConfig tag)
    {
        // Auto-configure based on tag name
        if (tag.Name.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorSettings { WaveType = "Sine", BaseValue = 25, Amplitude = 10, PeriodSeconds = 60, NoiseLevel = 0.5 };
        }
        if (tag.Name.Contains("Pressure", StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorSettings { WaveType = "Ramp", BaseValue = 5, Amplitude = 2, PeriodSeconds = 30, NoiseLevel = 0.1 };
        }
        if (tag.Name.Contains("Level", StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorSettings { WaveType = "Triangle", BaseValue = 50, Amplitude = 30, PeriodSeconds = 90, NoiseLevel = 0.2 };
        }
        if (tag.Name.Contains("Flow", StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatorSettings { WaveType = "Random", BaseValue = 150, Amplitude = 20, PeriodSeconds = 1, NoiseLevel = 5 };
        }
        if (tag.Name.Contains("Status", StringComparison.OrdinalIgnoreCase) || tag.DataType == "Boolean")
        {
            return new SimulatorSettings { WaveType = "Boolean", PeriodSeconds = 15 };
        }

        return new SimulatorSettings { WaveType = "Sine", BaseValue = 50, Amplitude = 25, PeriodSeconds = 60, NoiseLevel = 1 };
    }

    public ValueTask DisposeAsync()
    {
        _tagStates.Clear();
        return ValueTask.CompletedTask;
    }

    private class SimulatorTagState
    {
        public string TagId { get; set; } = string.Empty;
        public SimulatorSettings Settings { get; set; } = new();
        public string DataType { get; set; } = "Float";
        public DateTime StartTime { get; set; }
    }
}
