using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DataForeman.FlowEngine;

/// <summary>
/// Extension methods for registering flow engine services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds flow engine services to the service collection.
    /// </summary>
    public static IServiceCollection AddFlowEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FlowEngineOptions>(
            configuration.GetSection(FlowEngineOptions.SectionName));

        services.AddSingleton<IFlowExecutionEngine, FlowExecutionEngine>();
        services.AddHostedService<FlowEngineHostedService>();

        return services;
    }

    /// <summary>
    /// Adds flow engine services with custom configuration.
    /// </summary>
    public static IServiceCollection AddFlowEngine(this IServiceCollection services, Action<FlowEngineOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IFlowExecutionEngine, FlowExecutionEngine>();
        services.AddHostedService<FlowEngineHostedService>();

        return services;
    }
}
