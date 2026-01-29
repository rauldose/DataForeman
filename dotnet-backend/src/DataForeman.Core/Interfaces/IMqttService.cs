namespace DataForeman.Core.Interfaces;

/// <summary>
/// Service for MQTT messaging
/// </summary>
public interface IMqttService
{
    /// <summary>
    /// Connect to MQTT broker
    /// </summary>
    Task ConnectAsync(string brokerAddress, int port = 1883, string? username = null, string? password = null);
    
    /// <summary>
    /// Disconnect from MQTT broker
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Check if connected to broker
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Subscribe to a topic
    /// </summary>
    Task SubscribeAsync(string topic, MqttQualityOfService qos = MqttQualityOfService.AtMostOnce);
    
    /// <summary>
    /// Unsubscribe from a topic
    /// </summary>
    Task UnsubscribeAsync(string topic);
    
    /// <summary>
    /// Publish a message to a topic
    /// </summary>
    Task PublishAsync(string topic, string payload, MqttQualityOfService qos = MqttQualityOfService.AtMostOnce, bool retain = false);
    
    /// <summary>
    /// Publish a message to a topic
    /// </summary>
    Task PublishAsync(string topic, byte[] payload, MqttQualityOfService qos = MqttQualityOfService.AtMostOnce, bool retain = false);
    
    /// <summary>
    /// Event raised when a message is received
    /// </summary>
    event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;
    
    /// <summary>
    /// Event raised when connection state changes
    /// </summary>
    event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;
}

public enum MqttQualityOfService
{
    AtMostOnce = 0,
    AtLeastOnce = 1,
    ExactlyOnce = 2
}

public class MqttMessageReceivedEventArgs : EventArgs
{
    public string Topic { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string PayloadString => System.Text.Encoding.UTF8.GetString(Payload);
    public MqttQualityOfService QualityOfService { get; set; }
    public bool Retain { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MqttConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
