namespace DataForeman.Shared.Messages;

/// <summary>
/// Message sent from Engine to App containing flow execution trace information
/// </summary>
public class FlowExecutionMessage
{
    public string FlowId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO"; // INFO, WARN, ERROR
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? InputData { get; set; }
    public Dictionary<string, object>? OutputData { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
