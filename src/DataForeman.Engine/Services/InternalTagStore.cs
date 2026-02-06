using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// In-memory store for internal tags with context scopes (global, flow, node).
/// Similar to Node-RED's context store functionality.
/// </summary>
public class InternalTagStore : IDisposable
{
    private readonly ILogger<InternalTagStore> _logger;
    private readonly ConfigService _configService;
    
    // Thread-safe storage for tag values by scope
    // Key format: "global:{path}", "flow:{flowId}:{path}", "node:{flowId}:{nodeId}:{path}"
    private readonly ConcurrentDictionary<string, InternalTagValue> _values = new();
    
    // Registered tag configurations
    private readonly ConcurrentDictionary<string, InternalTagConfig> _configs = new();

    // Persistence
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private Timer? _flushTimer;
    private volatile bool _dirty;
    private string? _persistPath;

    public InternalTagStore(ILogger<InternalTagStore> logger, ConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Initializes the internal tag store with configured tags.
    /// Loads persisted global-scope values from disk if available.
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing internal tag store");

        _persistPath = Path.Combine(_configService.ConfigDirectory, "internal-tags.json");

        // Restore persisted values
        if (File.Exists(_persistPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_persistPath);
                var saved = JsonSerializer.Deserialize<Dictionary<string, InternalTagValue>>(json, _jsonOpts);
                if (saved is not null)
                {
                    foreach (var kvp in saved)
                        _values[kvp.Key] = kvp.Value;

                    _logger.LogInformation("Restored {Count} persisted context values from {Path}",
                        saved.Count, _persistPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load persisted context store from {Path}; starting empty", _persistPath);
            }
        }

        // Debounced flush timer — runs every 500 ms but only writes when dirty
        _flushTimer = new Timer(async _ =>
        {
            try { await FlushIfDirtyAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Error in context store flush timer"); }
        }, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        
        _logger.LogInformation("Internal tag store initialized with {Count} tags", _values.Count);
    }

    #region Global Context Operations

    /// <summary>
    /// Gets a value from global context.
    /// </summary>
    public InternalTagValue? GetGlobal(string path)
    {
        var key = BuildKey(ContextScope.Global, path);
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a value in global context.
    /// </summary>
    public void SetGlobal(string path, object? value)
    {
        var key = BuildKey(ContextScope.Global, path);
        var tagValue = new InternalTagValue
        {
            Path = path,
            Value = value,
            TimestampUtc = DateTime.UtcNow,
            Quality = 0,
            Scope = ContextScope.Global
        };
        _values[key] = tagValue;
        _dirty = true;
        
        _logger.LogDebug("Set global context '{Path}' = {Value}", path, value);
    }

    /// <summary>
    /// Gets all keys in global context.
    /// </summary>
    public IEnumerable<string> GetGlobalKeys()
    {
        var prefix = "global:";
        return _values.Keys
            .Where(k => k.StartsWith(prefix))
            .Select(k => k[prefix.Length..]);
    }

    #endregion

    #region Flow Context Operations

    /// <summary>
    /// Gets a value from flow context.
    /// </summary>
    public InternalTagValue? GetFlow(string flowId, string path)
    {
        var key = BuildKey(ContextScope.Flow, path, flowId);
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a value in flow context.
    /// </summary>
    public void SetFlow(string flowId, string path, object? value)
    {
        var key = BuildKey(ContextScope.Flow, path, flowId);
        var tagValue = new InternalTagValue
        {
            Path = path,
            Value = value,
            TimestampUtc = DateTime.UtcNow,
            Quality = 0,
            Scope = ContextScope.Flow
        };
        _values[key] = tagValue;
        
        _logger.LogDebug("Set flow context '{FlowId}/{Path}' = {Value}", flowId, path, value);
    }

    /// <summary>
    /// Gets all keys in a flow context.
    /// </summary>
    public IEnumerable<string> GetFlowKeys(string flowId)
    {
        var prefix = $"flow:{flowId}:";
        return _values.Keys
            .Where(k => k.StartsWith(prefix))
            .Select(k => k[prefix.Length..]);
    }

    /// <summary>
    /// Clears all values in a flow context.
    /// </summary>
    public void ClearFlow(string flowId)
    {
        var prefix = $"flow:{flowId}:";
        var keysToRemove = _values.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _values.TryRemove(key, out _);
        }
        
        _logger.LogDebug("Cleared flow context for '{FlowId}', removed {Count} keys", flowId, keysToRemove.Count);
    }

    #endregion

    #region Node Context Operations

    /// <summary>
    /// Gets a value from node context.
    /// </summary>
    public InternalTagValue? GetNode(string flowId, string nodeId, string path)
    {
        var key = BuildKey(ContextScope.Node, path, flowId, nodeId);
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a value in node context.
    /// </summary>
    public void SetNode(string flowId, string nodeId, string path, object? value)
    {
        var key = BuildKey(ContextScope.Node, path, flowId, nodeId);
        var tagValue = new InternalTagValue
        {
            Path = path,
            Value = value,
            TimestampUtc = DateTime.UtcNow,
            Quality = 0,
            Scope = ContextScope.Node
        };
        _values[key] = tagValue;
        
        _logger.LogDebug("Set node context '{FlowId}/{NodeId}/{Path}' = {Value}", flowId, nodeId, path, value);
    }

    /// <summary>
    /// Gets all keys in a node context.
    /// </summary>
    public IEnumerable<string> GetNodeKeys(string flowId, string nodeId)
    {
        var prefix = $"node:{flowId}:{nodeId}:";
        return _values.Keys
            .Where(k => k.StartsWith(prefix))
            .Select(k => k[prefix.Length..]);
    }

    /// <summary>
    /// Clears all values in a node context.
    /// </summary>
    public void ClearNode(string flowId, string nodeId)
    {
        var prefix = $"node:{flowId}:{nodeId}:";
        var keysToRemove = _values.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _values.TryRemove(key, out _);
        }
        
        _logger.LogDebug("Cleared node context for '{FlowId}/{NodeId}', removed {Count} keys", 
            flowId, nodeId, keysToRemove.Count);
    }

    #endregion

    #region Generic Operations

    /// <summary>
    /// Gets a value by full qualified path (e.g., "global.myvar", "flow.myflow.counter").
    /// </summary>
    public InternalTagValue? Get(string qualifiedPath)
    {
        var (scope, flowId, nodeId, path) = ParseQualifiedPath(qualifiedPath);
        
        return scope switch
        {
            ContextScope.Global => GetGlobal(path),
            ContextScope.Flow when flowId != null => GetFlow(flowId, path),
            ContextScope.Node when flowId != null && nodeId != null => GetNode(flowId, nodeId, path),
            _ => null
        };
    }

    /// <summary>
    /// Sets a value by full qualified path.
    /// </summary>
    public void Set(string qualifiedPath, object? value)
    {
        var (scope, flowId, nodeId, path) = ParseQualifiedPath(qualifiedPath);
        
        switch (scope)
        {
            case ContextScope.Global:
                SetGlobal(path, value);
                break;
            case ContextScope.Flow when flowId != null:
                SetFlow(flowId, path, value);
                break;
            case ContextScope.Node when flowId != null && nodeId != null:
                SetNode(flowId, nodeId, path, value);
                break;
            default:
                _logger.LogWarning("Invalid qualified path: '{Path}'", qualifiedPath);
                break;
        }
    }

    /// <summary>
    /// Gets the total count of stored values.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// Gets all stored values (for debugging/diagnostics).
    /// </summary>
    public IReadOnlyDictionary<string, InternalTagValue> GetAllValues() => _values;

    #endregion

    #region Helper Methods

    private static string BuildKey(ContextScope scope, string path, string? flowId = null, string? nodeId = null)
    {
        return scope switch
        {
            ContextScope.Global => $"global:{path}",
            ContextScope.Flow => $"flow:{flowId}:{path}",
            ContextScope.Node => $"node:{flowId}:{nodeId}:{path}",
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
    }

    private static (ContextScope scope, string? flowId, string? nodeId, string path) ParseQualifiedPath(string qualifiedPath)
    {
        // Expected formats:
        // "global.myvar" or "global:myvar"
        // "flow.myflowid.myvar" or "flow:myflowid:myvar"
        // "node.myflowid.mynodeid.myvar" or "node:myflowid:mynodeid:myvar"
        
        // Normalize separators
        var normalized = qualifiedPath.Replace(".", ":");
        var parts = normalized.Split(':', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
        {
            // Default to global scope
            return (ContextScope.Global, null, null, qualifiedPath);
        }

        var scopeStr = parts[0].ToLowerInvariant();
        
        return scopeStr switch
        {
            "global" => (ContextScope.Global, null, null, string.Join(":", parts.Skip(1))),
            "flow" when parts.Length >= 3 => (ContextScope.Flow, parts[1], null, string.Join(":", parts.Skip(2))),
            "node" when parts.Length >= 4 => (ContextScope.Node, parts[1], parts[2], string.Join(":", parts.Skip(3))),
            _ => (ContextScope.Global, null, null, qualifiedPath) // Default to global
        };
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Writes global-scope values to disk if anything changed since last flush.
    /// Called on a timer; safe to call from any thread.
    /// </summary>
    private async Task FlushIfDirtyAsync()
    {
        if (!_dirty || _persistPath is null) return;
        _dirty = false;

        try
        {
            // Only persist global-scope entries (flow/node scopes are ephemeral)
            var toSave = _values
                .Where(kvp => kvp.Key.StartsWith("global:"))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var json = JsonSerializer.Serialize(toSave, _jsonOpts);
            await File.WriteAllTextAsync(_persistPath, json);
            _logger.LogDebug("Flushed {Count} global context values to {Path}", toSave.Count, _persistPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist context store to {Path}", _persistPath);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;

        // Final synchronous flush so we don't lose pending changes
        if (_dirty && _persistPath is not null)
        {
            try
            {
                var toSave = _values
                    .Where(kvp => kvp.Key.StartsWith("global:"))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var json = JsonSerializer.Serialize(toSave, _jsonOpts);
                File.WriteAllText(_persistPath, json);
            }
            catch
            {
                // Best-effort flush during shutdown
            }
        }
    }

    #endregion
}
