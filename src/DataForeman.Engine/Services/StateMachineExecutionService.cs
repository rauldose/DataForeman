using System.Collections.Concurrent;
using System.Globalization;
using DataForeman.Engine.Drivers;
using DataForeman.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DataForeman.Engine.Services;

/// <summary>
/// Provides read access to the latest polled tag values so that state-machine
/// triggers can evaluate conditions without coupling directly to PollEngine.
/// </summary>
public interface IStateMachineTagReader
{
    /// <summary>Looks up the most recent value for a tag path ("ConnectionName/TagName").</summary>
    TagValue? GetCurrentTagValue(string tagPath);
}

/// <summary>
/// Provides write access to tags so that state-machine actions can set values
/// on external devices.
/// </summary>
public interface IStateMachineTagWriter
{
    /// <summary>Writes a value to the tag identified by "ConnectionName/TagName".</summary>
    Task WriteTagValueAsync(string tagPath, object value);
}

/// <summary>
/// Service for executing state machines.
/// Evaluates tag-based trigger conditions on a periodic scan cycle and
/// executes tag-write actions on state entry, exit, and transitions.
/// </summary>
public class StateMachineExecutionService : IDisposable
{
    private readonly ILogger<StateMachineExecutionService> _logger;
    private readonly IStateMachineTagReader? _tagReader;
    private readonly IStateMachineTagWriter? _tagWriter;
    private readonly CSharpScriptService? _scriptService;
    private readonly IFlowRunner? _flowRunner;
    private readonly Dictionary<string, StateMachineRuntime> _runtimes = new();
    private readonly object _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _scanTimer;
    private bool _disposed;

    /// <summary>Raised after any machine processes an event (regardless of success).</summary>
    public event Action<MachineRuntimeInfo>? RuntimeInfoUpdated;

    public StateMachineExecutionService(
        ILogger<StateMachineExecutionService> logger,
        IStateMachineTagReader? tagReader = null,
        IStateMachineTagWriter? tagWriter = null,
        CSharpScriptService? scriptService = null,
        IFlowRunner? flowRunner = null)
    {
        _logger = logger;
        _tagReader = tagReader;
        _tagWriter = tagWriter;
        _scriptService = scriptService;
        _flowRunner = flowRunner;
    }

    /// <summary>
    /// Starts a periodic scan timer that evaluates all trigger conditions
    /// on loaded state machines.  The <paramref name="intervalMs"/> controls
    /// how often the scan runs (default 500 ms).
    /// </summary>
    public void StartScanTimer(int intervalMs = 500)
    {
        _scanTimer?.Dispose();
        _scanTimer = new Timer(_ =>
        {
            try { ScanAllTriggers(); }
            catch (Exception ex) { _logger.LogError(ex, "Error during state machine trigger scan"); }
        }, null, intervalMs, intervalMs);
        _logger.LogInformation("State machine trigger scan started ({Interval}ms)", intervalMs);
    }

    /// <summary>Stops the periodic trigger scan.</summary>
    public void StopScanTimer()
    {
        _scanTimer?.Dispose();
        _scanTimer = null;
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

            var runtime = new StateMachineRuntime(config, _logger, _tagReader, _tagWriter, _scriptService, _flowRunner, _cts.Token);
            _runtimes[config.Id] = runtime;
            _logger.LogInformation("Loaded state machine: {Name} with {StateCount} states, {TransitionCount} transitions",
                config.Name, config.States.Count, config.Transitions.Count);
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
    /// Fires a named event to trigger state transitions.
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

    /// <summary>
    /// Evaluates every transition that has a <see cref="TagTrigger"/> on all
    /// loaded machines.  If a trigger condition becomes true the transition
    /// fires automatically (the Event field is used as the audit label).
    /// </summary>
    private void ScanAllTriggers()
    {
        if (_tagReader == null) return;

        List<MachineRuntimeInfo>? changedSnapshots = null;

        lock (_lock)
        {
            foreach (var runtime in _runtimes.Values)
            {
                if (runtime.EvaluateTriggersAndTransition())
                {
                    changedSnapshots ??= new List<MachineRuntimeInfo>();
                    changedSnapshots.Add(runtime.BuildRuntimeInfo());
                }
            }
        }

        // Publish outside the lock to avoid blocking the scan
        if (changedSnapshots != null)
        {
            foreach (var snapshot in changedSnapshots)
            {
                RuntimeInfoUpdated?.Invoke(snapshot);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _scanTimer?.Dispose();
            _cts.Dispose();
            _disposed = true;
        }
    }

    // ─── Inner runtime class ────────────────────────────────────────────

    private class StateMachineRuntime
    {
        private readonly StateMachineConfig _config;
        private readonly ILogger _logger;
        private readonly IStateMachineTagReader? _tagReader;
        private readonly IStateMachineTagWriter? _tagWriter;
        private readonly CSharpScriptService? _scriptService;
        private readonly IFlowRunner? _flowRunner;
        private readonly CancellationToken _ct;
        private readonly Dictionary<string, object?> _scriptState = new();
        private string? _currentStateId;
        private string? _prevStateId;
        private string? _recentTrigger;
        private bool _recentOutcome;
        private DateTime _lastChangeUtc = DateTime.UtcNow;
        private readonly List<TransitionAuditEntry> _auditLog = new();
        private const int AuditLimit = 80;
        private const double FloatEpsilon = 1e-9;

        public string? CurrentStateId => _currentStateId;

        public StateMachineRuntime(
            StateMachineConfig config,
            ILogger logger,
            IStateMachineTagReader? tagReader,
            IStateMachineTagWriter? tagWriter,
            CSharpScriptService? scriptService,
            IFlowRunner? flowRunner,
            CancellationToken ct)
        {
            _config = config;
            _logger = logger;
            _tagReader = tagReader;
            _tagWriter = tagWriter;
            _scriptService = scriptService;
            _flowRunner = flowRunner;
            _ct = ct;

            // Initialize to the designated initial state
            _currentStateId = config.InitialStateId
                ?? config.States.FirstOrDefault(s => s.IsInitial)?.Id;

            if (_currentStateId == null && config.States.Count > 0)
            {
                _currentStateId = config.States[0].Id;
            }

            _logger.LogDebug("State machine {Name} initialized to state {StateId}",
                config.Name, _currentStateId);
        }

        /// <summary>
        /// Checks all transitions from the current state that have a
        /// <see cref="TagTrigger"/>.  Returns true if any transition fired.
        /// </summary>
        public bool EvaluateTriggersAndTransition()
        {
            if (_currentStateId == null) return false;

            // Include transitions that have either a tag trigger or a script condition
            var candidates = _config.Transitions
                .Where(t => t.FromStateId == _currentStateId
                    && (t.Trigger != null || !string.IsNullOrEmpty(t.ScriptCondition)))
                .OrderBy(t => t.Priority)
                .ToList();

            foreach (var transition in candidates)
            {
                bool conditionMet = false;

                // Script condition takes priority if both are set
                if (!string.IsNullOrEmpty(transition.ScriptCondition) && _scriptService != null)
                {
                    conditionMet = Task.Run(() =>
                        _scriptService.EvaluateConditionAsync(transition.ScriptCondition, _scriptState))
                        .GetAwaiter().GetResult();
                }
                else if (transition.Trigger != null && _tagReader != null)
                {
                    conditionMet = EvaluateTagTrigger(transition.Trigger);
                }

                if (conditionMet)
                {
                    var label = !string.IsNullOrEmpty(transition.Event)
                        ? transition.Event
                        : transition.Trigger != null
                            ? FormatTriggerLabel(transition.Trigger)
                            : "script";

                    PerformTransition(transition, label);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fires a named event — finds transitions from the current state
        /// whose Event field matches, evaluates legacy string conditions
        /// and structured triggers, then transitions if valid.
        /// </summary>
        public bool FireEvent(string eventName, Dictionary<string, object>? context)
        {
            _recentTrigger = eventName;

            if (_currentStateId == null)
            {
                _logger.LogWarning("Cannot fire event {Event} on {Name}: no current state",
                    eventName, _config.Name);
                _recentOutcome = false;
                return false;
            }

            var transitions = _config.Transitions
                .Where(t => t.FromStateId == _currentStateId && t.Event == eventName)
                .OrderBy(t => t.Priority)
                .ToList();

            foreach (var transition in transitions)
            {
                // Check structured trigger if present
                if (transition.Trigger != null && !EvaluateTagTrigger(transition.Trigger))
                    continue;

                // Legacy string condition (backward-compat)
                if (!string.IsNullOrEmpty(transition.Condition))
                {
                    if (!EvaluateLegacyCondition(transition.Condition, context))
                        continue;
                }

                PerformTransition(transition, eventName);
                return true;
            }

            _recentOutcome = false;
            _logger.LogDebug("No valid transition for event {Event} in state {State}",
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

        // ── Transition execution ──────────────────────────────────────

        private void PerformTransition(StateTransition transition, string triggerLabel)
        {
            var oldStateId = _currentStateId!;
            var oldStateName = LookupStateName(oldStateId);
            var newStateId = transition.ToStateId;
            var newStateName = LookupStateName(newStateId);

            // 1. Execute on-exit actions of the source state
            var oldState = _config.States.FirstOrDefault(s => s.Id == oldStateId);
            if (oldState?.OnExitActions.Count > 0)
                RunTagActions(oldState.OnExitActions, "OnExit", oldStateName);
            if (!string.IsNullOrEmpty(oldState?.OnExitScript))
                RunScript(oldState.OnExitScript, "OnExitScript", oldStateName);
            if (oldState?.OnExitFlowIds.Count > 0)
                TriggerFlows(oldState.OnExitFlowIds, "OnExit", oldStateName);

            // 2. Execute transition actions
            if (transition.Actions.Count > 0)
                RunTagActions(transition.Actions, "Transition", triggerLabel);
            if (!string.IsNullOrEmpty(transition.ScriptAction))
                RunScript(transition.ScriptAction, "TransitionScript", triggerLabel);
            if (transition.FlowIds.Count > 0)
                TriggerFlows(transition.FlowIds, "Transition", triggerLabel);

            // Legacy single-string action (backward-compat)
            if (!string.IsNullOrEmpty(transition.Action))
                _logger.LogDebug("Legacy action on transition: {Action}", transition.Action);

            // 3. Perform the state change
            _prevStateId = oldStateId;
            _currentStateId = newStateId;
            _recentTrigger = triggerLabel;
            _recentOutcome = true;
            _lastChangeUtc = DateTime.UtcNow;

            // 4. Execute on-enter actions of the destination state
            var newState = _config.States.FirstOrDefault(s => s.Id == newStateId);
            if (newState?.OnEnterActions.Count > 0)
                RunTagActions(newState.OnEnterActions, "OnEnter", newStateName);
            if (!string.IsNullOrEmpty(newState?.OnEnterScript))
                RunScript(newState.OnEnterScript, "OnEnterScript", newStateName);
            if (newState?.OnEnterFlowIds.Count > 0)
                TriggerFlows(newState.OnEnterFlowIds, "OnEnter", newStateName);

            // 5. Audit trail
            _auditLog.Add(new TransitionAuditEntry
            {
                SrcId = oldStateId,
                SrcName = oldStateName,
                DstId = newStateId,
                DstName = newStateName,
                Trigger = triggerLabel,
                When = _lastChangeUtc
            });
            if (_auditLog.Count > AuditLimit)
                _auditLog.RemoveAt(0);

            _logger.LogInformation(
                "State machine {Name}: {OldState} → {NewState} [trigger: {Trigger}]",
                _config.Name, oldStateName, newStateName, triggerLabel);
        }

        // ── Tag trigger evaluation ────────────────────────────────────

        private bool EvaluateTagTrigger(TagTrigger trigger)
        {
            if (_tagReader == null || string.IsNullOrEmpty(trigger.TagPath))
                return false;

            var tagValue = _tagReader.GetCurrentTagValue(trigger.TagPath);
            if (tagValue?.Value == null)
                return false;

            // Convert both sides to double for numeric comparison
            if (TryParseDouble(tagValue.Value, out var actual)
                && TryParseDouble(trigger.Threshold, out var threshold))
            {
                return trigger.Operator switch
                {
                    TriggerOperator.Eq  => Math.Abs(actual - threshold) < FloatEpsilon,
                    TriggerOperator.Neq => Math.Abs(actual - threshold) >= FloatEpsilon,
                    TriggerOperator.Gt  => actual > threshold,
                    TriggerOperator.Gte => actual >= threshold,
                    TriggerOperator.Lt  => actual < threshold,
                    TriggerOperator.Lte => actual <= threshold,
                    _ => false
                };
            }

            // Fall back to string comparison for non-numeric values
            var actualStr = tagValue.Value.ToString() ?? "";
            var thresholdStr = trigger.Threshold;
            return trigger.Operator switch
            {
                TriggerOperator.Eq  => string.Equals(actualStr, thresholdStr, StringComparison.OrdinalIgnoreCase),
                TriggerOperator.Neq => !string.Equals(actualStr, thresholdStr, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private bool EvaluateLegacyCondition(string condition, Dictionary<string, object>? context)
        {
            _logger.LogDebug("Evaluating legacy condition: {Condition}", condition);
            // Legacy conditions still pass through — they will be true unless
            // the context explicitly provides a "false" sentinel.
            if (context != null
                && context.TryGetValue(condition, out var val)
                && val is bool b)
            {
                return b;
            }
            return true;
        }

        // ── Tag action execution ──────────────────────────────────────

        private void RunTagActions(List<TagAction> actions, string phase, string context)
        {
            if (_tagWriter == null) return;

            foreach (var action in actions)
            {
                if (string.IsNullOrEmpty(action.TagPath)) continue;

                try
                {
                    object writeValue = ParseActionValue(action.Value);
                    var tagPath = action.TagPath;
                    var val = action.Value;
                    // Fire-and-forget with exception observation (actions must not block the scan)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _tagWriter.WriteTagValueAsync(tagPath, writeValue);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to write {Value} to {Tag} during {Phase}({Context})",
                                val, tagPath, phase, context);
                        }
                    });

                    _logger.LogDebug("{Phase}({Context}): writing {Value} → {Tag}",
                        phase, context, action.Value, action.TagPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing {Phase} action for tag {Tag}", phase, action.TagPath);
                }
            }
        }

        // ── C# script execution ───────────────────────────────────────

        private void RunScript(string code, string phase, string context)
        {
            if (_scriptService == null) return;

            try
            {
                var result = Task.Run(() =>
                    _scriptService.ExecuteAsync(code, _scriptState, input: null, timeoutMs: 5000))
                    .GetAwaiter().GetResult();

                foreach (var msg in result.LogMessages)
                    _logger.LogInformation("[Script {Phase}({Context})] {Message}", phase, context, msg);

                if (!result.Success)
                    _logger.LogWarning("Script {Phase}({Context}) failed: {Error}", phase, context, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running script {Phase}({Context})", phase, context);
            }
        }

        // ── Flow triggering ───────────────────────────────────────────

        private void TriggerFlows(List<string> flowIds, string phase, string context)
        {
            if (_flowRunner == null || flowIds.Count == 0) return;

            var machineName = _config.Name;
            foreach (var flowId in flowIds)
            {
                if (string.IsNullOrEmpty(flowId)) continue;

                var capturedId = flowId;
                var triggerSource = $"StateMachine:{machineName}/{phase}({context})";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _flowRunner.TriggerFlowAsync(capturedId, triggerSource);
                    }
                    catch (OperationCanceledException) { /* shutdown */ }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to trigger flow {FlowId} during {Phase}({Context})",
                            capturedId, phase, context);
                    }
                }, _ct);

                _logger.LogDebug("{Phase}({Context}): triggering flow {FlowId}", phase, context, flowId);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private string LookupStateName(string id)
            => _config.States.FirstOrDefault(s => s.Id == id)?.Name ?? id;

        private static string FormatTriggerLabel(TagTrigger trigger)
        {
            var opSymbol = trigger.Operator switch
            {
                TriggerOperator.Eq  => "==",
                TriggerOperator.Neq => "!=",
                TriggerOperator.Gt  => ">",
                TriggerOperator.Gte => ">=",
                TriggerOperator.Lt  => "<",
                TriggerOperator.Lte => "<=",
                _ => "?"
            };
            return $"{trigger.TagPath} {opSymbol} {trigger.Threshold}";
        }

        private static bool TryParseDouble(object? value, out double result)
        {
            if (value is double d) { result = d; return true; }
            if (value is float f) { result = f; return true; }
            if (value is int i) { result = i; return true; }
            if (value is long l) { result = l; return true; }
            if (value is decimal dec) { result = (double)dec; return true; }
            if (value is bool bv) { result = bv ? 1.0 : 0.0; return true; }
            if (value is string s)
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            result = 0;
            return false;
        }

        private static object ParseActionValue(string value)
        {
            if (bool.TryParse(value, out var b)) return b;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv)) return iv;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv)) return dv;
            return value;
        }
    }
}
