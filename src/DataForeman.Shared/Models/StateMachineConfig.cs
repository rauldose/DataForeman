using System.Text.Json.Serialization;

namespace DataForeman.Shared.Models;

/// <summary>
/// State machine configuration for modeling system states and transitions.
/// </summary>
public class StateMachineConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = false;
    public string? InitialStateId { get; set; }
    public List<MachineState> States { get; set; } = new();
    public List<StateTransition> Transitions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A state in the state machine.
/// </summary>
public class MachineState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6"; // Default blue
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// A transition between two states.
/// </summary>
public class StateTransition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromStateId { get; set; } = string.Empty;
    public string ToStateId { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? Action { get; set; }
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Root configuration file structure for state machines.
/// </summary>
public class StateMachinesFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public List<StateMachineConfig> StateMachines { get; set; } = new();
}
