namespace DataForeman.App.Services;

/// <summary>
/// Background service that generates simulated data when the Engine is offline.
/// This allows the UI to demonstrate charts and dashboards without needing the full Engine running.
/// </summary>
public class SimulatedDataService : BackgroundService
{
    private readonly ILogger<SimulatedDataService> _logger;
    private readonly RealtimeDataService _realtimeData;
    private readonly ConfigService _configService;
    private readonly Random _random = new();
    
    // Simulation state (maintains continuity between updates)
    private readonly Dictionary<string, double> _currentValues = new();
    private readonly Dictionary<string, double> _trends = new();

    public SimulatedDataService(
        RealtimeDataService realtimeData,
        ConfigService configService,
        ILogger<SimulatedDataService> logger)
    {
        _realtimeData = realtimeData;
        _configService = configService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimulatedDataService started - generating test data every 1 second");
        
        // Wait for config to load
        await Task.Delay(2000, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                GenerateSimulatedValues();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating simulated data");
            }
            
            await Task.Delay(1000, stoppingToken); // Update every 1 second
        }
    }

    private void GenerateSimulatedValues()
    {
        foreach (var connection in _configService.Connections)
        {
            foreach (var tag in connection.Tags)
            {
                var tagId = tag.Id;
                var key = $"{connection.Id}:{tagId}";
                
                // Initialize value if needed
                if (!_currentValues.ContainsKey(key))
                {
                    _currentValues[key] = GetInitialValue(tag.Name);
                    _trends[key] = _random.NextDouble() * 0.4 - 0.2; // Random trend between -0.2 and +0.2
                }
                
                // Update value with some randomness and trend
                var currentValue = _currentValues[key];
                var trend = _trends[key];
                var noise = (_random.NextDouble() - 0.5) * GetNoiseAmplitude(tag.Name);
                
                currentValue += trend + noise;
                
                // Apply bounds based on tag type
                currentValue = ApplyBounds(tag.Name, currentValue);
                
                // Occasionally reverse trend
                if (_random.NextDouble() < 0.05)
                {
                    _trends[key] = -_trends[key];
                }
                
                _currentValues[key] = currentValue;
                
                // Inject the simulated value
                _realtimeData.InjectTestValue(tagId, tag.Name, connection.Id, Math.Round(currentValue, 2));
            }
        }
    }

    private double GetInitialValue(string tagName)
    {
        var name = tagName.ToLower();
        return name switch
        {
            var n when n.Contains("temp") => 22 + _random.NextDouble() * 6, // 22-28°C
            var n when n.Contains("pressure") => 4 + _random.NextDouble() * 2, // 4-6 bar
            var n when n.Contains("level") => 40 + _random.NextDouble() * 20, // 40-60%
            var n when n.Contains("flow") => 200 + _random.NextDouble() * 100, // 200-300 L/min
            _ => 50 + _random.NextDouble() * 20
        };
    }

    private double GetNoiseAmplitude(string tagName)
    {
        var name = tagName.ToLower();
        return name switch
        {
            var n when n.Contains("temp") => 0.5,
            var n when n.Contains("pressure") => 0.2,
            var n when n.Contains("level") => 1.0,
            var n when n.Contains("flow") => 5.0,
            _ => 1.0
        };
    }

    private double ApplyBounds(string tagName, double value)
    {
        var name = tagName.ToLower();
        return name switch
        {
            var n when n.Contains("temp") => Math.Clamp(value, 15, 40), // 15-40°C
            var n when n.Contains("pressure") => Math.Clamp(value, 0, 10), // 0-10 bar
            var n when n.Contains("level") => Math.Clamp(value, 0, 100), // 0-100%
            var n when n.Contains("flow") => Math.Clamp(value, 0, 500), // 0-500 L/min
            _ => Math.Clamp(value, 0, 100)
        };
    }
}
