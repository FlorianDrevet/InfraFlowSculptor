using InfraFlowSculptor.Api.Common.Mapping;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Authentication;
using InfraFlowSculptor.Api.Common;
using Microsoft.AspNetCore.Authorization;

namespace InfraFlowSculptor.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddMapping();
        services.AddScoped<IdempotencyEndpointFilter>();
        services.AddIfsAuthentication(configuration, environment);
        services.AddAuthorization(options =>
        {
            var memberPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(AuthorizationPolicies.Member, memberPolicy);
            options.FallbackPolicy = memberPolicy;
        });
        return services;
    }
}
