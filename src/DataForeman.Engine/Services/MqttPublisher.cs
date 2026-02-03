using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using DataForeman.Shared.Models;

namespace DataForeman.Engine.Services;

/// <summary>
/// MQTT publisher for sending tag values and engine status
/// </summary>
public class MqttPublisher : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IManagedMqttClient? _client;
    private bool _isConnected;

    public event Func<EngineCommandMessage, Task>? OnCommand;
    public event Func<HistoryRequestMessage, Task>? OnHistoryRequest;

    public bool IsConnected => _isConnected;

    public MqttPublisher(IConfiguration configuration, ILogger<MqttPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task ConnectAsync()
    {
        var broker = _configuration.GetValue<string>("Mqtt:Broker") ?? "localhost";
        var port = _configuration.GetValue<int>("Mqtt:Port", 1883);
        var clientId = $"dataforeman-engine-{Environment.MachineName}";

        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithTcpServer(broker, port)
                .WithClientId(clientId)
                .WithCleanSession()
                .Build())
            .Build();

        _client = new MqttFactory().CreateManagedMqttClient();

        _client.ConnectedAsync += async e =>
        {
            _isConnected = true;
            _logger.LogInformation("Connected to MQTT broker at {Broker}:{Port}", broker, port);

            // Subscribe to commands
            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.Command)
                .Build());

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.HistoryRequest)
                .Build());
        };

        _client.DisconnectedAsync += e =>
        {
            _isConnected = false;
            _logger.LogWarning("Disconnected from MQTT broker");
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        await _client.StartAsync(options);
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (topic == MqttTopics.Command)
            {
                var cmd = JsonSerializer.Deserialize<EngineCommandMessage>(payload, _jsonOptions);
                if (cmd != null && OnCommand != null)
                    await OnCommand(cmd);
            }
            else if (topic == MqttTopics.HistoryRequest)
            {
                var req = JsonSerializer.Deserialize<HistoryRequestMessage>(payload, _jsonOptions);
                if (req != null && OnHistoryRequest != null)
                    await OnHistoryRequest(req);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic {Topic}", e.ApplicationMessage.Topic);
        }
    }

    /// <summary>
    /// Publish a tag value update
    /// </summary>
    public void PublishTagValue(TagValueMessage msg)
    {
        if (_client == null || !_isConnected)
            return;

        var topic = MqttTopics.GetTagTopic(msg.ConnectionId, msg.TagId);
        var payload = JsonSerializer.Serialize(msg, _jsonOptions);

        _client.EnqueueAsync(topic, payload, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
    }

    /// <summary>
    /// Publish engine status
    /// </summary>
    public async Task PublishStatusAsync(EngineStatusMessage status)
    {
        if (_client == null || !_isConnected)
            return;

        var payload = JsonSerializer.Serialize(status, _jsonOptions);
        await _client.EnqueueAsync(MqttTopics.EngineStatus, payload);
    }

    /// <summary>
    /// Publish historical data response
    /// </summary>
    public async Task PublishHistoryAsync(TagHistoryMessage history)
    {
        if (_client == null || !_isConnected)
            return;

        var topic = MqttTopics.GetHistoryTopic(history.ConnectionId, history.TagId);
        var payload = JsonSerializer.Serialize(history, _jsonOptions);
        await _client.EnqueueAsync(topic, payload);
    }

    /// <summary>
    /// Publish flow execution event
    /// </summary>
    public async Task PublishFlowExecutionAsync(FlowExecutionMessage msg)
    {
        if (_client == null || !_isConnected)
            return;

        var topic = MqttTopics.GetFlowTopic(msg.FlowId);
        var payload = JsonSerializer.Serialize(msg, _jsonOptions);
        await _client.EnqueueAsync(topic, payload);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
    }
}
