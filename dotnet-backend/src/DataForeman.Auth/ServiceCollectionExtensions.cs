using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DataForeman.Auth;

/// <summary>
/// Extension methods for registering authentication services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DataForeman authentication and authorization services.
    /// </summary>
    public static IServiceCollection AddDataForemanAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure JWT options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // Add JWT token service
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Configure JWT authentication
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
            });

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Admin-only policy
            options.AddPolicy(Policies.AdminOnly, policy =>
                policy.RequireRole(Roles.Admin));

            // Dashboard management policy
            options.AddPolicy(Policies.DashboardManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.Dashboards}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.Dashboards}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.Dashboards}:{PermissionTypes.Delete}")));

            // Flow management policy
            options.AddPolicy(Policies.FlowManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.Flows}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.Flows}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.Flows}:{PermissionTypes.Delete}")));

            // Connectivity management policy
            options.AddPolicy(Policies.ConnectivityManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityDevices}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityDevices}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityDevices}:{PermissionTypes.Delete}")));

            // Tag management policy
            options.AddPolicy(Policies.TagManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityTags}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityTags}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.ConnectivityTags}:{PermissionTypes.Delete}")));

            // Chart management policy
            options.AddPolicy(Policies.ChartManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.ChartComposer}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.ChartComposer}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.ChartComposer}:{PermissionTypes.Delete}")));

            // User management policy
            options.AddPolicy(Policies.UserManagement, policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.Admin) ||
                    context.User.HasClaim("permission", $"{Features.Users}:{PermissionTypes.Create}") ||
                    context.User.HasClaim("permission", $"{Features.Users}:{PermissionTypes.Update}") ||
                    context.User.HasClaim("permission", $"{Features.Users}:{PermissionTypes.Delete}")));

            // Read-only policy
            options.AddPolicy(Policies.ReadOnly, policy =>
                policy.RequireAuthenticatedUser());
        });

        return services;
    }
}
