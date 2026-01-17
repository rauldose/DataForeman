using MQTTnet;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace DataForeman.Connectivity;

public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    private IMqttClient? _mqttClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttSettings = configuration.GetSection("Mqtt");
        var broker = mqttSettings["Broker"] ?? "localhost";
        var port = int.Parse(mqttSettings["Port"] ?? "1883");
        var clientId = mqttSettings["ClientId"] ?? $"dataforeman-connectivity-{Guid.NewGuid():N}";

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();
            
            logger.LogInformation("Received message on topic {Topic}: {Payload}", topic, payload);
            
            // Process incoming telemetry data
            await ProcessTelemetryAsync(topic, payload ?? "", stoppingToken);
        };

        _mqttClient.DisconnectedAsync += async e =>
        {
            logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}", e.Reason);
            
            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await ConnectAsync(options, stoppingToken);
            }
        };

        await ConnectAsync(options, stoppingToken);

        // Keep the worker running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ConnectAsync(MqttClientOptions options, CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Connecting to MQTT broker...");
            await _mqttClient!.ConnectAsync(options, stoppingToken);
            logger.LogInformation("Connected to MQTT broker");

            // Subscribe to telemetry topics
            await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("dataforeman/telemetry/#")
                .WithTopicFilter("dataforeman/status/#")
                .WithTopicFilter("dataforeman/commands/#")
                .Build(), stoppingToken);

            logger.LogInformation("Subscribed to telemetry topics");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to MQTT broker");
        }
    }

    private async Task ProcessTelemetryAsync(string topic, string payload, CancellationToken stoppingToken)
    {
        try
        {
            // Parse the topic to determine the message type
            var topicParts = topic.Split('/');
            
            if (topicParts.Length >= 3 && topicParts[1] == "telemetry")
            {
                var deviceId = topicParts[2];
                logger.LogDebug("Processing telemetry from device {DeviceId}", deviceId);
                
                // Here you would:
                // 1. Parse the payload (JSON, raw values, etc.)
                // 2. Map to tag values
                // 3. Store in time-series database
                // 4. Publish to real-time subscribers
            }
            else if (topicParts.Length >= 3 && topicParts[1] == "status")
            {
                var deviceId = topicParts[2];
                logger.LogDebug("Processing status update from device {DeviceId}", deviceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing telemetry message");
        }
        
        await Task.CompletedTask;
    }

    public async Task PublishAsync(string topic, object message, CancellationToken stoppingToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            logger.LogWarning("Cannot publish - not connected to MQTT broker");
            return;
        }

        var payload = JsonSerializer.Serialize(message);
        
        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(mqttMessage, stoppingToken);
        logger.LogDebug("Published message to {Topic}", topic);
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), stoppingToken);
            logger.LogInformation("Disconnected from MQTT broker");
        }

        await base.StopAsync(stoppingToken);
    }
}
