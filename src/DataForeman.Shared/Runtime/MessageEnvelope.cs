// DataForeman Platform - AI Agent Implementation Directives
// Section 3: MESSAGE MODEL (FLOW CURRENCY)
// Messages are immutable. New messages are emitted, not mutated.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataForeman.Shared.Runtime;

/// <summary>
/// Immutable message envelope exchanged between nodes.
/// All nodes receive and emit MessageEnvelope instances.
/// CorrelationId is preserved across flows and subflows.
/// </summary>
public sealed record MessageEnvelope
{
    /// <summary>Unique identifier for this message instance.</summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }

    /// <summary>Correlation ID preserved across the entire flow execution chain.</summary>
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    /// <summary>UTC timestamp when this message was created.</summary>
    [JsonPropertyName("createdUtc")]
    public required DateTime CreatedUtc { get; init; }

    /// <summary>Key/value headers for metadata.</summary>
    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>JSON payload data.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>Source node ID that emitted this message.</summary>
    [JsonPropertyName("sourceNodeId")]
    public string? SourceNodeId { get; init; }

    /// <summary>Source port name from which this message was emitted.</summary>
    [JsonPropertyName("sourcePort")]
    public string? SourcePort { get; init; }

    /// <summary>
    /// Creates a new message with a generated MessageId.
    /// Uses the provided time provider for deterministic testing.
    /// </summary>
    public static MessageEnvelope Create(
        string correlationId,
        DateTime createdUtc,
        JsonElement? payload = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? sourceNodeId = null,
        string? sourcePort = null)
    {
        return new MessageEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            CreatedUtc = createdUtc,
            Payload = payload,
            Headers = headers ?? new Dictionary<string, string>(),
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort
        };
    }

    /// <summary>
    /// Creates a new correlation chain starting message.
    /// </summary>
    public static MessageEnvelope CreateNew(DateTime createdUtc, JsonElement? payload = null)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        return Create(correlationId, createdUtc, payload);
    }

    /// <summary>
    /// Derives a new message preserving the correlation chain.
    /// The original message is not mutated.
    /// </summary>
    public MessageEnvelope Derive(
        DateTime createdUtc,
        JsonElement? payload = null,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        string? sourceNodeId = null,
        string? sourcePort = null)
    {
        var newHeaders = new Dictionary<string, string>(Headers);
        if (additionalHeaders != null)
        {
            foreach (var kvp in additionalHeaders)
            {
                newHeaders[kvp.Key] = kvp.Value;
            }
        }

        return new MessageEnvelope
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = CorrelationId, // Preserved!
            CreatedUtc = createdUtc,
            Payload = payload ?? Payload,
            Headers = newHeaders,
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort
        };
    }

    /// <summary>
    /// Gets the payload as a typed object.
    /// </summary>
    public T? GetPayload<T>()
    {
        if (Payload == null || Payload.Value.ValueKind == JsonValueKind.Null)
            return default;

        return Payload.Value.Deserialize<T>();
    }

    /// <summary>
    /// Creates a payload from an object.
    /// </summary>
    public static JsonElement CreatePayload<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
