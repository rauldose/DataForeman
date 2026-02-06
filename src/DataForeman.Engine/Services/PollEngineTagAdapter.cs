using DataForeman.Engine.Drivers;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// Bridges PollEngine (keyed by TagId) to the ITagValueProvider / ITagValueWriter
/// interfaces used by StateMachineExecutionService (keyed by "ConnectionName/TagName").
/// </summary>
public sealed class PollEngineTagAdapter : IStateMachineTagReader, IStateMachineTagWriter
{
    private readonly PollEngine _pollEngine;
    private readonly ConfigService _configService;
    private readonly ILogger<PollEngineTagAdapter> _logger;

    public PollEngineTagAdapter(
        PollEngine pollEngine,
        ConfigService configService,
        ILogger<PollEngineTagAdapter> logger)
    {
        _pollEngine = pollEngine;
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TagValue? GetCurrentTagValue(string tagPath)
    {
        var (connection, tag) = ResolveTag(tagPath);
        if (tag == null) return null;

        return _pollEngine.CurrentValues.TryGetValue(tag.Id, out var value)
            ? value
            : null;
    }

    /// <inheritdoc/>
    public async Task WriteTagValueAsync(string tagPath, object value)
    {
        var (connection, tag) = ResolveTag(tagPath);
        if (connection == null || tag == null)
        {
            _logger.LogWarning("Cannot write to unknown tag path: {TagPath}", tagPath);
            return;
        }

        // Delegate to PollEngine's write path
        await _pollEngine.WriteTagAsync(connection.Id, tag, value);
    }

    /// <summary>
    /// Resolves a "ConnectionName/TagName" path to the matching
    /// ConnectionConfig + TagConfig pair.
    /// </summary>
    private (ConnectionConfig? Connection, TagConfig? Tag) ResolveTag(string tagPath)
    {
        if (string.IsNullOrEmpty(tagPath)) return (null, null);

        var separatorIndex = tagPath.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= tagPath.Length - 1)
            return (null, null);

        var connectionName = tagPath.Substring(0, separatorIndex);
        var tagName = tagPath.Substring(separatorIndex + 1);

        var connection = _configService.Connections
            .FirstOrDefault(c => string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));
        if (connection == null) return (null, null);

        var tag = connection.Tags
            .FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));

        return (connection, tag);
    }
}
