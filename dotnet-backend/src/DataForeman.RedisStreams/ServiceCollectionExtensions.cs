using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DataForeman.RedisStreams;

/// <summary>
/// Extension methods for registering Redis Streams services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis Streams services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisStreams(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisConnectionOptions>(
            configuration.GetSection(RedisConnectionOptions.SectionName));

        services.AddSingleton<IRedisStreamService, RedisStreamService>();

        return services;
    }

    /// <summary>
    /// Adds Redis Streams services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisStreams(this IServiceCollection services, Action<RedisConnectionOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IRedisStreamService, RedisStreamService>();

        return services;
    }
}
