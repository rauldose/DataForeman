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
/// A point-in-time capture of where a running state machine sits,
/// plus the last few transitions that occurred.
/// </summary>
public class MachineRuntimeInfo
{
    public string ConfigId { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public string? NowStateId { get; set; }
    public string? NowStateName { get; set; }
    public string? BeforeStateId { get; set; }
    public string? BeforeStateName { get; set; }
    public string? LastTrigger { get; set; }
    public bool WasSuccessful { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime SnapshotUtc { get; set; } = DateTime.UtcNow;
    public List<TransitionAuditEntry> Audit { get; set; } = new();
}

/// <summary>
/// Single entry in a state machine's transition audit trail.
/// </summary>
public class TransitionAuditEntry
{
    public string SrcId { get; set; } = string.Empty;
    public string SrcName { get; set; } = string.Empty;
    public string DstId { get; set; } = string.Empty;
    public string DstName { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public DateTime When { get; set; } = DateTime.UtcNow;
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
