using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Services;

/// <summary>
/// Adapter that wraps InternalTagStore to implement IContextStore for a specific flow/node context.
/// This allows nodes to access context without knowing the underlying implementation.
/// </summary>
public sealed class ContextStoreAdapter : IContextStore
{
    private readonly InternalTagStore _store;
    private readonly string? _flowId;
    private readonly string? _nodeId;

    public ContextStoreAdapter(InternalTagStore store, string? flowId = null, string? nodeId = null)
    {
        _store = store;
        _flowId = flowId;
        _nodeId = nodeId;
    }

    #region Global Context

    public object? GetGlobal(string key)
    {
        return _store.GetGlobal(key)?.Value;
    }

    public void SetGlobal(string key, object? value)
    {
        _store.SetGlobal(key, value);
    }

    public IEnumerable<string> GetGlobalKeys()
    {
        return _store.GetGlobalKeys();
    }

    #endregion

    #region Flow Context

    public object? GetFlow(string key)
    {
        if (string.IsNullOrEmpty(_flowId))
        {
            return null;
        }
        return _store.GetFlow(_flowId, key)?.Value;
    }

    public void SetFlow(string key, object? value)
    {
        if (string.IsNullOrEmpty(_flowId))
        {
            return;
        }
        _store.SetFlow(_flowId, key, value);
    }

    public IEnumerable<string> GetFlowKeys()
    {
        if (string.IsNullOrEmpty(_flowId))
        {
            return Enumerable.Empty<string>();
        }
        return _store.GetFlowKeys(_flowId);
    }

    #endregion

    #region Node Context

    public object? GetNode(string key)
    {
        if (string.IsNullOrEmpty(_flowId) || string.IsNullOrEmpty(_nodeId))
        {
            return null;
        }
        return _store.GetNode(_flowId, _nodeId, key)?.Value;
    }

    public void SetNode(string key, object? value)
    {
        if (string.IsNullOrEmpty(_flowId) || string.IsNullOrEmpty(_nodeId))
        {
            return;
        }
        _store.SetNode(_flowId, _nodeId, key, value);
    }

    public IEnumerable<string> GetNodeKeys()
    {
        if (string.IsNullOrEmpty(_flowId) || string.IsNullOrEmpty(_nodeId))
        {
            return Enumerable.Empty<string>();
        }
        return _store.GetNodeKeys(_flowId, _nodeId);
    }

    #endregion
}
