// DataForeman Platform - AI Agent Implementation Directives
// Node Registry and Runtime Factory implementations.

using DataForeman.Shared.Definition;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Runtime;

/// <summary>
/// Default node registry implementation.
/// </summary>
public sealed class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, NodeDescriptor> _descriptors = new();
    private readonly Dictionary<string, Func<INodeRuntime>> _runtimeFactories = new();

    /// <summary>
    /// Registers a node type with its descriptor and runtime factory.
    /// </summary>
    public void Register(NodeDescriptor descriptor, Func<INodeRuntime> runtimeFactory)
    {
        _descriptors[descriptor.Type] = descriptor;
        _runtimeFactories[descriptor.Type] = runtimeFactory;
    }

    /// <summary>
    /// Registers a node type using the runtime type.
    /// </summary>
    public void Register<TRuntime>(NodeDescriptor descriptor) where TRuntime : INodeRuntime, new()
    {
        Register(descriptor, () => new TRuntime());
    }

    public NodeDescriptor? GetDescriptor(string nodeType)
    {
        return _descriptors.TryGetValue(nodeType, out var descriptor) ? descriptor : null;
    }

    public IReadOnlyList<NodeDescriptor> GetAllDescriptors()
    {
        return _descriptors.Values.ToList();
    }

    public IReadOnlyList<NodeDescriptor> GetDescriptorsByCategory(string category)
    {
        return _descriptors.Values.Where(d => d.Category == category).ToList();
    }

    public bool IsRegistered(string nodeType)
    {
        return _descriptors.ContainsKey(nodeType);
    }

    /// <summary>
    /// Gets all registered categories.
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        return _descriptors.Values.Select(d => d.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Creates a runtime instance for a node type.
    /// </summary>
    public INodeRuntime CreateRuntime(string nodeType)
    {
        if (!_runtimeFactories.TryGetValue(nodeType, out var factory))
            throw new InvalidOperationException($"No runtime factory registered for node type: {nodeType}");

        return factory();
    }
}

/// <summary>
/// Node runtime factory using the registry.
/// </summary>
public sealed class RegistryRuntimeFactory : INodeRuntimeFactory
{
    private readonly NodeRegistry _registry;

    public RegistryRuntimeFactory(NodeRegistry registry)
    {
        _registry = registry;
    }

    public INodeRuntime CreateRuntime(string nodeType)
    {
        return _registry.CreateRuntime(nodeType);
    }
}
