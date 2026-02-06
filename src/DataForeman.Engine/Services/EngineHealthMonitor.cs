namespace DataForeman.Engine.Services;

/// <summary>
/// Centralized health monitor for the Engine.
/// Collects health signals from subsystems and exposes an aggregate status.
/// The Worker logs a periodic health summary and the status is included in
/// <see cref="DataForeman.Shared.Mqtt.EngineStatusMessage"/> for UI display.
/// </summary>
public sealed class EngineHealthMonitor
{
    private readonly ILogger<EngineHealthMonitor> _logger;

    // Individual subsystem status flags — written by the owning services.
    private volatile bool _mqttConnected;
    private volatile bool _pollEngineRunning;
    private volatile bool _configLoaded;
    private volatile int _compiledFlowCount;
    private volatile int _loadedStateMachineCount;
    private DateTime _lastHealthCheckUtc = DateTime.UtcNow;

    public EngineHealthMonitor(ILogger<EngineHealthMonitor> logger)
    {
        _logger = logger;
    }

    // ── Mutators (called by individual services) ──────────────

    public void SetMqttConnected(bool connected) => _mqttConnected = connected;
    public void SetPollEngineRunning(bool running) => _pollEngineRunning = running;
    public void SetConfigLoaded(bool loaded) => _configLoaded = loaded;
    public void SetCompiledFlowCount(int count) => _compiledFlowCount = count;
    public void SetLoadedStateMachineCount(int count) => _loadedStateMachineCount = count;

    // ── Queries ───────────────────────────────────────────────

    public bool IsMqttConnected => _mqttConnected;
    public bool IsPollEngineRunning => _pollEngineRunning;
    public bool IsConfigLoaded => _configLoaded;
    public int CompiledFlowCount => _compiledFlowCount;
    public int LoadedStateMachineCount => _loadedStateMachineCount;

    /// <summary>
    /// Returns true when all critical subsystems are operational.
    /// </summary>
    public bool IsHealthy =>
        _mqttConnected && _pollEngineRunning && _configLoaded;

    /// <summary>
    /// Produces a human-readable summary of the current health state.
    /// </summary>
    public string BuildSummary()
    {
        var parts = new List<string>(6);
        parts.Add(_mqttConnected ? "MQTT=OK" : "MQTT=DOWN");
        parts.Add(_pollEngineRunning ? "Poll=OK" : "Poll=STOPPED");
        parts.Add(_configLoaded ? "Config=OK" : "Config=FAIL");
        parts.Add($"Flows={_compiledFlowCount}");
        parts.Add($"SM={_loadedStateMachineCount}");
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Logs a health summary at the appropriate level.
    /// Intended to be called on a timer by the Worker.
    /// </summary>
    public void LogHealthStatus()
    {
        _lastHealthCheckUtc = DateTime.UtcNow;
        var summary = BuildSummary();

        if (IsHealthy)
            _logger.LogInformation("Engine health: HEALTHY — {Summary}", summary);
        else
            _logger.LogWarning("Engine health: DEGRADED — {Summary}", summary);
    }
}
