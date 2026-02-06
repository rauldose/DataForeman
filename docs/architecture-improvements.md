# Architecture Improvements — Bulletproof DataForeman

> Audit performed against the Engine (C# worker service), App (Blazor Server),
> and Shared library.  Items are prioritized P0 → P3.

---

## 1  State Persistence  (P0 — data loss on restart)

### Problem
All runtime state lives in-memory `ConcurrentDictionary` / `Dictionary`:

| Data | Location | Lost on restart? |
|------|----------|:---:|
| Internal tags (context store) | `InternalTagStore._values` | ✅ |
| State machine current state + audit | `StateMachineExecutionService._runtimes` | ✅ |
| Flow run history | `RealtimeDataService._flowRunHistory` | ✅ |
| Tag value cache (UI) | `RealtimeDataService._tagValues` | ✅ |

### Recommended fix
Add a **write-ahead file store** per concern:

```
config/
  internal-tags.json          ← persisted context store
  state-machine-runtime.json  ← current state per machine
```

* **InternalTagStore** — flush to `internal-tags.json` on every write (debounced
  250 ms).  Load on startup.  This is the most critical item because flows rely
  on context state.
* **StateMachineExecutionService** — persist `MachineRuntimeInfo` snapshots.
  Reload last-known state on startup instead of always resetting to initial.
* Both stores already exist in the Engine; the persistence layer only needs a
  `Save` and `Load` pair.

### Why not Redis?
The current MQTT bus is already sufficient for Engine→App sync.  Redis adds
operational complexity (another service to monitor) and is only warranted at
**multi-Engine horizontal-scale** deployments.  For a single-Engine setup,
file-based persistence is simpler, has zero dependencies, and survives restarts.

---

## 2  Async Exception Safety  (P0 — crash risk)

### Problem
`async void` patterns and fire-and-forget timers can crash the process on
unhandled exceptions:

| File | Issue |
|------|-------|
| `PollEngine.cs` L66 | `Timer(async _ => await PublishEngineStatusAsync())` — no try/catch |
| `PollEngine.cs` L167 | `async void OnTagValuesReceived(...)` — catches but still async void |
| `PollEngine.cs` L218 | `async void OnConnectionStatusChanged(...)` — same |
| `FlowExecutionService.cs` L301 | `async void HandleFlowTriggered(...)` — has try/catch, acceptable |

### Recommended fix
Wrap every `async void` callback in a try/catch that logs + swallows:

```csharp
_statusTimer = new Timer(async _ =>
{
    try { await PublishEngineStatusAsync(); }
    catch (Exception ex) { _logger.LogError(ex, "Status publish failed"); }
}, ...);
```

---

## 3  Health Checks  (P1 — operability)

### Problem
No `/health` endpoint exists in either the Engine or the App.  In production
there is no way for an orchestrator (Docker, K8s, systemd watchdog) to know
whether the system is healthy.

### Recommended architecture

```
Engine (BackgroundService)
  └─ IHealthCheck implementations:
       • MqttHealthCheck     → is broker connected?
       • PollEngineHealthCheck → are all pollers running?
       • ConfigHealthCheck   → did config load without errors?

App (Blazor Server)
  └─ /health endpoint (MapHealthChecks)
       • MqttHealthCheck     → is MQTT connected?
       • EngineStatusHealthCheck → received engine heartbeat < 30 s ago?
```

ASP.NET has built-in `Microsoft.Extensions.Diagnostics.HealthChecks`; the
Engine can expose a minimal Kestrel endpoint on a management port or simply
log health status periodically.

---

## 4  Configuration Validation  (P1 — silent bad config)

### Problem
Both `ConfigService` implementations deserialize JSON with no post-load
validation.  Malformed or incomplete config silently creates empty objects:

```csharp
_connections = JsonSerializer.Deserialize<ConnectionsFile>(json) ?? new ConnectionsFile();
```

### Recommended fix
Add a `Validate()` method on each config model:

```csharp
public class ConnectionConfig
{
    // ... existing props ...
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Connection name is required");
        if (PollRateMs < 10) errors.Add("Poll rate must be >= 10ms");
        // ...
        return errors;
    }
}
```

Call after deserialization; log warnings for each validation error but still
load the config (fail-open with warnings).

---

## 5  Graceful Degradation & Circuit Breaker  (P1)

### Problem
If the MQTT broker goes offline, the Engine continues polling tags and building
up an unbounded publish queue inside `ManagedMqttClient`.

### Recommended fix
* Monitor `ManagedMqttClient.PendingApplicationMessagesCount` and log a warning
  when it exceeds a threshold (e.g. 1 000 messages).
* If the backlog exceeds a hard limit, drop the oldest messages to prevent
  unbounded memory growth.
* Expose this metric in the `EngineStatusMessage` so the UI can show a warning.

---

## 6  Idempotent MQTT State Messages  (P2)

### Current design (good)
State machine snapshots are published with `retain = true` and `QoS = 1`,
which means the broker holds the last-known state and delivers it to any new
subscriber.  This is correct.

### Improvement
Add a monotonic `SequenceNumber` to `MachineRuntimeInfo` and
`FlowRunSummaryMessage` so the App can detect out-of-order delivery and
discard stale messages:

```csharp
public class MachineRuntimeInfo
{
    public long Seq { get; set; }   // incremented on every state change
    // ...
}
```

The App-side handler simply checks `if (incoming.Seq <= cached.Seq) return;`.

---

## 7  Structured Observability  (P2)

### Problem
Logging is structured but there is no metrics pipeline (counters, histograms).

### Recommended additions

| Metric | Type | Source |
|--------|------|--------|
| `dataforeman_tags_polled_total` | Counter | PollEngine |
| `dataforeman_poll_duration_ms` | Histogram | PollEngine |
| `dataforeman_mqtt_publish_total` | Counter | MqttPublisher |
| `dataforeman_mqtt_pending_messages` | Gauge | MqttPublisher |
| `dataforeman_flow_executions_total` | Counter | FlowExecutionService |
| `dataforeman_flow_duration_ms` | Histogram | FlowExecutionService |
| `dataforeman_sm_transitions_total` | Counter | StateMachineExecutionService |

Use `System.Diagnostics.Metrics` (built into .NET) or OpenTelemetry.  Expose
via Prometheus endpoint for Grafana dashboards.

---

## 8  Configuration Hot-Reload Safety  (P2)

### Problem
`ConfigWatcher` detects file changes and triggers reload, but there is no
versioning or atomic swap of the in-memory config objects.  A race exists if
a flow execution reads config while a reload is mid-flight.

### Recommended fix
* Use an immutable snapshot pattern: `ConfigService` produces a new
  `ConfigSnapshot` record on each reload and consumers hold a reference to
  the snapshot they started with.
* Add a config version counter so the UI can detect when it is out of sync.

---

## 9  Multi-Instance / Horizontal Scale  (P3 — future)

When scaling beyond a single Engine instance:

* **MQTT is already the right bus** — multiple Engines can publish to the same
  broker with distinct client IDs.
* **Shared state** (internal tags, state machines) would then need Redis or a
  distributed store.  Until then, file persistence is sufficient.
* **Flow execution** needs a distributed lock (e.g. Redis `SETNX`) to prevent
  duplicate execution when multiple Engines subscribe to the same trigger.

---

## Summary — Priority Order

| # | Item | Effort | Impact |
|---|------|--------|--------|
| 1 | State persistence (internal tags + SM) | Medium | Prevents data loss |
| 2 | Async exception safety | Small | Prevents crashes |
| 3 | Health checks | Medium | Enables monitoring |
| 4 | Config validation | Small | Prevents silent failures |
| 5 | MQTT backlog protection | Small | Prevents OOM |
| 6 | Idempotent state messages | Small | Prevents stale UI |
| 7 | Structured metrics | Medium | Enables dashboards |
| 8 | Config hot-reload safety | Medium | Prevents race conditions |
| 9 | Multi-instance support | Large | Horizontal scaling |
