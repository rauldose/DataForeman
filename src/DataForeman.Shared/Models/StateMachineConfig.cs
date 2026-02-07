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
    public string? OwnerId { get; set; }
    public bool IsShared { get; set; }
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

    /// <summary>Actions executed when the machine enters this state.</summary>
    public List<TagAction> OnEnterActions { get; set; } = new();

    /// <summary>Actions executed when the machine leaves this state.</summary>
    public List<TagAction> OnExitActions { get; set; } = new();

    /// <summary>C# script executed when entering this state. Has access to ReadTag/WriteTag/Log.</summary>
    public string? OnEnterScript { get; set; }

    /// <summary>C# script executed when leaving this state.</summary>
    public string? OnExitScript { get; set; }

    /// <summary>Flow IDs to start when entering this state.</summary>
    public List<string> OnEnterFlowIds { get; set; } = new();

    /// <summary>Flow IDs to start when leaving this state.</summary>
    public List<string> OnExitFlowIds { get; set; } = new();
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

    /// <summary>Tag-based trigger condition that must be true for this transition to fire.</summary>
    public TagTrigger? Trigger { get; set; }

    /// <summary>Actions executed when this transition fires (after exiting source, before entering target).</summary>
    public List<TagAction> Actions { get; set; } = new();

    /// <summary>C# script that must return true/false for this transition to fire. Overrides Trigger if set.</summary>
    public string? ScriptCondition { get; set; }

    /// <summary>C# script executed when this transition fires. Has access to ReadTag/WriteTag/Log.</summary>
    public string? ScriptAction { get; set; }

    /// <summary>Flow IDs to start when this transition fires.</summary>
    public List<string> FlowIds { get; set; } = new();
}

/// <summary>
/// A tag-based condition that evaluates a comparison against a live tag value.
/// </summary>
public class TagTrigger
{
    /// <summary>Tag path in "ConnectionName/TagName" format.</summary>
    public string TagPath { get; set; } = string.Empty;

    /// <summary>Comparison operator: Eq, Neq, Gt, Gte, Lt, Lte.</summary>
    public TriggerOperator Operator { get; set; } = TriggerOperator.Eq;

    /// <summary>Threshold value to compare against (stored as string, parsed at evaluation time).</summary>
    public string Threshold { get; set; } = string.Empty;
}

/// <summary>
/// Comparison operators for tag trigger conditions.
/// </summary>
public enum TriggerOperator
{
    /// <summary>Equal (==)</summary>
    Eq,
    /// <summary>Not equal (!=)</summary>
    Neq,
    /// <summary>Greater than (&gt;)</summary>
    Gt,
    /// <summary>Greater than or equal (&gt;=)</summary>
    Gte,
    /// <summary>Less than (&lt;)</summary>
    Lt,
    /// <summary>Less than or equal (&lt;=)</summary>
    Lte
}

/// <summary>
/// An action that writes a value to a tag when executed.
/// </summary>
public class TagAction
{
    /// <summary>Tag path in "ConnectionName/TagName" format.</summary>
    public string TagPath { get; set; } = string.Empty;

    /// <summary>Value to write to the tag (stored as string, converted at execution time).</summary>
    public string Value { get; set; } = string.Empty;
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
