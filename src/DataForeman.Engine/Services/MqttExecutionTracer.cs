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
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, List<NodeExecutionResult>> _traces = new();

    public MqttExecutionTracer(MqttPublisher mqtt)
    {
        _mqtt = mqtt;
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
            
            // Fire and forget - don't block execution for MQTT publish
            _ = _mqtt.PublishMessageAsync(topic, payload);
        }
        catch (Exception ex)
        {
            // Don't let tracing errors affect flow execution
            Console.WriteLine($"[MqttExecutionTracer] Error publishing trace: {ex.Message}");
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
