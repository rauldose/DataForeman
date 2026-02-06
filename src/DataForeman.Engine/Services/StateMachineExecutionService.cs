using DataForeman.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DataForeman.Engine.Services;

/// <summary>
/// Service for executing state machines.
/// Monitors state transitions based on events and conditions.
/// </summary>
public class StateMachineExecutionService
{
    private readonly ILogger<StateMachineExecutionService> _logger;
    private readonly Dictionary<string, StateMachineRuntime> _runtimes = new();
    private readonly object _lock = new();

    /// <summary>Raised after any machine processes an event (regardless of success).</summary>
    public event Action<MachineRuntimeInfo>? RuntimeInfoUpdated;

    public StateMachineExecutionService(ILogger<StateMachineExecutionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads and initializes a state machine.
    /// </summary>
    public void LoadStateMachine(StateMachineConfig config)
    {
        lock (_lock)
        {
            if (!config.Enabled)
            {
                _logger.LogDebug("State machine {Name} is disabled, skipping load", config.Name);
                return;
            }

            var runtime = new StateMachineRuntime(config, _logger);
            _runtimes[config.Id] = runtime;
            _logger.LogInformation("Loaded state machine: {Name} with {StateCount} states", 
                config.Name, config.States.Count);
        }
    }

    /// <summary>
    /// Unloads a state machine.
    /// </summary>
    public void UnloadStateMachine(string id)
    {
        lock (_lock)
        {
            if (_runtimes.Remove(id))
            {
                _logger.LogInformation("Unloaded state machine: {Id}", id);
            }
        }
    }

    /// <summary>
    /// Fires an event to trigger state transitions.
    /// </summary>
    public bool FireEvent(string stateMachineId, string eventName, Dictionary<string, object>? context = null)
    {
        lock (_lock)
        {
            if (_runtimes.TryGetValue(stateMachineId, out var runtime))
            {
                var result = runtime.FireEvent(eventName, context);
                RuntimeInfoUpdated?.Invoke(runtime.BuildRuntimeInfo());
                return result;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the current state of a state machine.
    /// </summary>
    public string? GetCurrentState(string stateMachineId)
    {
        lock (_lock)
        {
            if (_runtimes.TryGetValue(stateMachineId, out var runtime))
            {
                return runtime.CurrentStateId;
            }
            return null;
        }
    }

    /// <summary>
    /// Builds a runtime info snapshot for one machine.
    /// </summary>
    public MachineRuntimeInfo? GetRuntimeInfo(string machineId)
    {
        lock (_lock)
        {
            return _runtimes.TryGetValue(machineId, out var rt)
                ? rt.BuildRuntimeInfo()
                : null;
        }
    }

    /// <summary>
    /// Builds runtime info snapshots for every loaded machine.
    /// </summary>
    public List<MachineRuntimeInfo> GetAllRuntimeInfo()
    {
        lock (_lock)
        {
            return _runtimes.Values.Select(rt => rt.BuildRuntimeInfo()).ToList();
        }
    }

    /// <summary>
    /// Reloads all state machines from configuration.
    /// </summary>
    public void ReloadAll(IEnumerable<StateMachineConfig> configs)
    {
        lock (_lock)
        {
            _runtimes.Clear();
            foreach (var config in configs)
            {
                LoadStateMachine(config);
            }
        }
    }

    private class StateMachineRuntime
    {
        private readonly StateMachineConfig _config;
        private readonly ILogger _logger;
        private string? _currentStateId;
        private string? _prevStateId;
        private string? _recentTrigger;
        private bool _recentOutcome;
        private DateTime _lastChangeUtc = DateTime.UtcNow;
        private readonly List<TransitionAuditEntry> _auditLog = new();
        private const int AuditLimit = 80;

        public string? CurrentStateId => _currentStateId;

        public StateMachineRuntime(StateMachineConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            
            // Initialize to the initial state
            _currentStateId = config.InitialStateId 
                ?? config.States.FirstOrDefault(s => s.IsInitial)?.Id;
            
            if (_currentStateId == null && config.States.Any())
            {
                _currentStateId = config.States[0].Id;
            }

            _logger.LogDebug("State machine {Name} initialized to state {StateId}", 
                config.Name, _currentStateId);
        }

        public bool FireEvent(string eventName, Dictionary<string, object>? context)
        {
            _recentTrigger = eventName;

            if (_currentStateId == null)
            {
                _logger.LogWarning("Cannot fire event {Event} on state machine {Name}: no current state",
                    eventName, _config.Name);
                _recentOutcome = false;
                return false;
            }

            // Find matching transitions from current state
            var transitions = _config.Transitions
                .Where(t => t.FromStateId == _currentStateId && t.Event == eventName)
                .OrderBy(t => t.Priority)
                .ToList();

            foreach (var transition in transitions)
            {
                // Check condition if specified
                if (!string.IsNullOrEmpty(transition.Condition))
                {
                    if (!EvaluateCondition(transition.Condition, context))
                    {
                        continue;
                    }
                }

                // Execute action if specified
                if (!string.IsNullOrEmpty(transition.Action))
                {
                    ExecuteAction(transition.Action, context);
                }

                // Transition to new state
                var oldState = _currentStateId;
                _prevStateId = oldState;
                _currentStateId = transition.ToStateId;
                _recentOutcome = true;
                _lastChangeUtc = DateTime.UtcNow;
                
                var oldStateName = _config.States.FirstOrDefault(s => s.Id == oldState)?.Name ?? oldState;
                var newStateName = _config.States.FirstOrDefault(s => s.Id == _currentStateId)?.Name ?? _currentStateId;

                // Append to audit trail
                _auditLog.Add(new TransitionAuditEntry
                {
                    SrcId = oldState, SrcName = oldStateName,
                    DstId = _currentStateId, DstName = newStateName,
                    Trigger = eventName, When = _lastChangeUtc
                });
                if (_auditLog.Count > AuditLimit)
                    _auditLog.RemoveAt(0);

                _logger.LogInformation("State machine {Name} transitioned from {OldState} to {NewState} on event {Event}",
                    _config.Name, oldStateName, newStateName, eventName);
                
                return true;
            }

            _recentOutcome = false;
            _logger.LogDebug("No valid transition found for event {Event} in state {State}",
                eventName, _currentStateId);
            return false;
        }

        /// <summary>Captures a snapshot of where this machine stands right now.</summary>
        public MachineRuntimeInfo BuildRuntimeInfo()
        {
            string LookupName(string? id) => id == null
                ? string.Empty
                : _config.States.FirstOrDefault(s => s.Id == id)?.Name ?? id;

            return new MachineRuntimeInfo
            {
                ConfigId = _config.Id,
                ConfigName = _config.Name,
                NowStateId = _currentStateId,
                NowStateName = LookupName(_currentStateId),
                BeforeStateId = _prevStateId,
                BeforeStateName = LookupName(_prevStateId),
                LastTrigger = _recentTrigger,
                WasSuccessful = _recentOutcome,
                ChangedAtUtc = _lastChangeUtc,
                SnapshotUtc = DateTime.UtcNow,
                Audit = _auditLog.TakeLast(40).ToList()
            };
        }

        private bool EvaluateCondition(string condition, Dictionary<string, object>? context)
        {
            // Simple condition evaluation - in production this would be more sophisticated
            // For now, just return true to allow all transitions
            _logger.LogDebug("Evaluating condition: {Condition}", condition);
            return true;
        }

        private void ExecuteAction(string action, Dictionary<string, object>? context)
        {
            // Execute action - in production this would trigger actual behaviors
            _logger.LogDebug("Executing action: {Action}", action);
        }
    }
}
