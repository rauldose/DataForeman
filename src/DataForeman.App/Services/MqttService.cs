using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using DataForeman.Shared.Models;

namespace DataForeman.App.Services;

/// <summary>
/// Service for MQTT communication with the Engine
/// </summary>
public class MqttService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IManagedMqttClient? _client;
    private bool _isConnected;

    public event Action<TagValueMessage>? OnTagValue;
    public event Action<TagHistoryMessage>? OnHistoryData;
    public event Action<EngineStatusMessage>? OnEngineStatus;
    public event Action<FlowExecutionMessage>? OnFlowExecution;
    public event Action<bool>? OnConnectionChanged;

    public bool IsConnected => _isConnected;

    public MqttService(IConfiguration configuration, ILogger<MqttService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task ConnectAsync()
    {
        var broker = _configuration.GetValue<string>("Mqtt:Broker") ?? "localhost";
        var port = _configuration.GetValue<int>("Mqtt:Port", 1883);
        var clientId = $"dataforeman-app-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];

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
            OnConnectionChanged?.Invoke(true);

            // Subscribe to topics
            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.TagValueWildcard)
                .Build());

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.EngineStatus)
                .Build());

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic(MqttTopics.FlowExecutionWildcard)
                .Build());

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic("dataforeman/history/#")
                .Build());
        };

        _client.DisconnectedAsync += e =>
        {
            _isConnected = false;
            _logger.LogWarning("Disconnected from MQTT broker");
            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        await _client.StartAsync(options);
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            // Route message based on topic
            if (topic.StartsWith("dataforeman/tags/"))
            {
                var msg = JsonSerializer.Deserialize<TagValueMessage>(payload, _jsonOptions);
                if (msg != null)
                    OnTagValue?.Invoke(msg);
            }
            else if (topic == MqttTopics.EngineStatus)
            {
                var msg = JsonSerializer.Deserialize<EngineStatusMessage>(payload, _jsonOptions);
                if (msg != null)
                    OnEngineStatus?.Invoke(msg);
            }
            else if (topic.StartsWith("dataforeman/flows/") && topic.EndsWith("/execution"))
            {
                var msg = JsonSerializer.Deserialize<FlowExecutionMessage>(payload, _jsonOptions);
                if (msg != null)
                    OnFlowExecution?.Invoke(msg);
            }
            else if (topic.StartsWith("dataforeman/history/") && !topic.EndsWith("/request"))
            {
                var msg = JsonSerializer.Deserialize<TagHistoryMessage>(payload, _jsonOptions);
                if (msg != null)
                    OnHistoryData?.Invoke(msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic {Topic}", e.ApplicationMessage.Topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Send a command to the Engine
    /// </summary>
    public async Task SendCommandAsync(EngineCommand command, Dictionary<string, object?>? parameters = null)
    {
        if (_client == null || !_isConnected)
            return;

        var msg = new EngineCommandMessage
        {
            Command = command,
            Parameters = parameters
        };

        var payload = JsonSerializer.Serialize(msg, _jsonOptions);
        await _client.EnqueueAsync(MqttTopics.Command, payload);
    }

    /// <summary>
    /// Request historical data for a tag
    /// </summary>
    public async Task RequestHistoryAsync(string connectionId, string tagId, DateTime start, DateTime end,
        int maxPoints = 1000, AggregationType aggregation = AggregationType.None)
    {
        if (_client == null || !_isConnected)
            return;

        var request = new HistoryRequestMessage
        {
            ConnectionId = connectionId,
            TagId = tagId,
            StartTime = new DateTimeOffset(start).ToUnixTimeMilliseconds(),
            EndTime = new DateTimeOffset(end).ToUnixTimeMilliseconds(),
            MaxPoints = maxPoints,
            Aggregation = aggregation
        };

        var payload = JsonSerializer.Serialize(request, _jsonOptions);
        await _client.EnqueueAsync(MqttTopics.HistoryRequest, payload);
    }

    /// <summary>
    /// Tell the engine to reload configuration
    /// </summary>
    public Task ReloadConfigAsync()
        => SendCommandAsync(EngineCommand.ReloadConfig);

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
    }
}
