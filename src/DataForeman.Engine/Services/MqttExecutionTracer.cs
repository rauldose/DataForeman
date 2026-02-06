using System.Collections.Concurrent;
using System.Text.Json;
using DataForeman.Shared.Messages;
using DataForeman.Shared.Runtime;

namespace DataForeman.Engine.Services;

/// <summary>
/// Publishes flow execution traces to MQTT for real-time UI display
/// </summary>
public class MqttExecutionTracer : IExecutionTracer
{
    private readonly MqttPublisher _mqtt;
    private readonly ILogger<MqttExecutionTracer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, List<NodeExecutionResult>> _traces = new();

    public MqttExecutionTracer(MqttPublisher mqtt, ILogger<MqttExecutionTracer> logger)
    {
        _mqtt = mqtt;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void RecordTrace(NodeExecutionResult trace)
    {
        // Store trace locally
        var traces = _traces.GetOrAdd(trace.RunId, _ => new List<NodeExecutionResult>());
        lock (traces)
        {
            traces.Add(trace);
        }

        // Publish to MQTT for real-time UI display
        try
        {
            if (!_mqtt.IsConnected) return;

            var message = new FlowExecutionMessage
            {
                FlowId = trace.RunId, // Use RunId as the flow execution identifier
                NodeId = trace.NodeId,
                NodeType = trace.NodeType,
                Level = trace.Status == ExecutionStatus.Failed ? "ERROR" : "INFO",
                Message = trace.Status == ExecutionStatus.Failed 
                    ? $"Failed: {trace.Error}" 
                    : $"Executed in {trace.Duration.TotalMilliseconds:F1}ms, emitted {trace.MessagesEmitted} messages",
                InputData = null,
                OutputData = null,
                Timestamp = trace.EndUtc
            };

            var topic = $"dataforeman/flows/{trace.RunId}/execution";
            var payload = JsonSerializer.Serialize(message, _jsonOptions);
            
            // Publish in background but observe exceptions
            _ = PublishTraceAsync(topic, payload);
        }
        catch (Exception ex)
        {
            // Don't let tracing errors affect flow execution
            _logger.LogWarning(ex, "Error publishing trace to MQTT for run {RunId}", trace.RunId);
        }
    }

    private async Task PublishTraceAsync(string topic, string payload)
    {
        try
        {
            await _mqtt.PublishMessageAsync(topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish execution trace to {Topic}", topic);
        }
    }

    public IReadOnlyList<NodeExecutionResult> GetTraces(string runId)
    {
        if (_traces.TryGetValue(runId, out var traces))
        {
            lock (traces)
            {
                return traces.ToList().AsReadOnly();
            }
        }
        return Array.Empty<NodeExecutionResult>();
    }

    public void ClearOldTraces(DateTime beforeUtc)
    {
        var keysToRemove = new List<string>();
        
        foreach (var kvp in _traces)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t.EndUtc < beforeUtc);
                if (kvp.Value.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            _traces.TryRemove(key, out _);
        }
    }
}
