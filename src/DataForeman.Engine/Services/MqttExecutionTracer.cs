using System.Text.Json;
using DataForeman.Shared.Messages;
using MQTTnet;
using MQTTnet.Protocol;

namespace DataForeman.Engine.Services;

/// <summary>
/// Publishes flow execution traces to MQTT for real-time UI display
/// </summary>
public class MqttExecutionTracer : IExecutionTracer
{
    private readonly MqttPublisher _mqtt;
    private readonly JsonSerializerOptions _jsonOptions;
    private string _currentFlowId = string.Empty;

    public MqttExecutionTracer(MqttPublisher mqtt)
    {
        _mqtt = mqtt;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void SetFlowId(string flowId)
    {
        _currentFlowId = flowId;
    }

    public void TraceNodeInput(string nodeId, string nodeType, Dictionary<string, object> inputs)
    {
        PublishTrace(nodeId, nodeType, "INFO", $"Received input", inputs, null);
    }

    public void TraceNodeOutput(string nodeId, string nodeType, Dictionary<string, object> outputs)
    {
        PublishTrace(nodeId, nodeType, "INFO", $"Produced output", null, outputs);
    }

    public void TraceNodeError(string nodeId, string nodeType, string error)
    {
        PublishTrace(nodeId, nodeType, "ERROR", error, null, null);
    }

    public void TraceNodeExecution(string nodeId, string nodeType, string message)
    {
        PublishTrace(nodeId, nodeType, "INFO", message, null, null);
    }

    private void PublishTrace(string nodeId, string nodeType, string level, string message, 
        Dictionary<string, object>? inputs, Dictionary<string, object>? outputs)
    {
        try
        {
            if (!_mqtt.IsConnected) return;

            var trace = new FlowExecutionMessage
            {
                FlowId = _currentFlowId,
                NodeId = nodeId,
                NodeType = nodeType,
                Level = level,
                Message = message,
                InputData = inputs,
                OutputData = outputs,
                Timestamp = DateTime.UtcNow
            };

            var topic = $"dataforeman/flows/{_currentFlowId}/execution";
            var payload = JsonSerializer.Serialize(trace, _jsonOptions);
            
            // Fire and forget - don't block execution for MQTT publish
            _ = _mqtt.PublishMessageAsync(topic, payload);
        }
        catch (Exception ex)
        {
            // Don't let tracing errors affect flow execution
            Console.WriteLine($"[MqttExecutionTracer] Error publishing trace: {ex.Message}");
        }
    }
}

/// <summary>
/// Interface for flow execution tracing
/// </summary>
public interface IExecutionTracer
{
    void SetFlowId(string flowId);
    void TraceNodeInput(string nodeId, string nodeType, Dictionary<string, object> inputs);
    void TraceNodeOutput(string nodeId, string nodeType, Dictionary<string, object> outputs);
    void TraceNodeError(string nodeId, string nodeType, string error);
    void TraceNodeExecution(string nodeId, string nodeType, string message);
}
