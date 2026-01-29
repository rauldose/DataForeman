using Microsoft.Extensions.Logging;

namespace DataForeman.Drivers;

/// <summary>
/// Factory for creating protocol driver instances.
/// </summary>
public interface IDriverFactory
{
    /// <summary>
    /// Create a driver instance from a configuration.
    /// </summary>
    IProtocolDriver CreateDriver(DriverConfigBase config);

    /// <summary>
    /// Get supported driver types.
    /// </summary>
    IEnumerable<string> GetSupportedDriverTypes();
}

/// <summary>
/// Default implementation of the driver factory.
/// </summary>
public class DriverFactory : IDriverFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the driver factory.
    /// </summary>
    public DriverFactory(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    {
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IProtocolDriver CreateDriver(DriverConfigBase config)
    {
        return config switch
        {
            OpcUaDriverConfig opcUaConfig => new OpcUaDriver(
                opcUaConfig,
                _loggerFactory.CreateLogger<OpcUaDriver>(),
                GetRedisService()),

            EtherNetIpDriverConfig eipConfig => new EtherNetIpDriver(
                eipConfig,
                _loggerFactory.CreateLogger<EtherNetIpDriver>(),
                GetRedisService()),

            S7DriverConfig s7Config => new S7Driver(
                s7Config,
                _loggerFactory.CreateLogger<S7Driver>(),
                GetRedisService()),

            _ => throw new ArgumentException($"Unknown driver configuration type: {config.GetType().Name}")
        };
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSupportedDriverTypes()
    {
        return new[]
        {
            OpcUaDriverConfig.DriverType,
            EtherNetIpDriverConfig.DriverType,
            S7DriverConfig.DriverType
        };
    }

    private DataForeman.RedisStreams.IRedisStreamService? GetRedisService()
    {
        return _serviceProvider.GetService(typeof(DataForeman.RedisStreams.IRedisStreamService)) 
            as DataForeman.RedisStreams.IRedisStreamService;
    }
}
