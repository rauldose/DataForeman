using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// Connection configuration for data sources.
/// </summary>
public class ConnectionConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Simulator"; // Simulator, OpcUa, S7, Modbus, etc.
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Settings { get; set; } = new();
    public List<TagConfig> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tag configuration within a connection.
/// </summary>
public class TagConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DataType { get; set; } = "Float"; // Boolean, Int16, Int32, Float, Double, String
    public int PollRateMs { get; set; } = 1000;
    public bool Enabled { get; set; } = true;
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public SimulatorSettings? Simulator { get; set; }
}

/// <summary>
/// Simulator-specific settings for a tag.
/// </summary>
public class SimulatorSettings
{
    public string WaveType { get; set; } = "Sine"; // Sine, Cosine, Ramp, Triangle, Step, Random, Boolean, Constant
    public double BaseValue { get; set; } = 50.0;
    public double Amplitude { get; set; } = 25.0;
    public double PeriodSeconds { get; set; } = 60.0;
    public double NoiseLevel { get; set; } = 1.0;
}

/// <summary>
/// Root configuration file structure for connections.
/// </summary>
public class ConnectionsFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<ConnectionConfig> Connections { get; set; } = new();
}
