using InfraFlowSculptor.Api.Common.Mapping;
using InfraFlowSculptor.Api.Common.Idempotency;

namespace InfraFlowSculptor.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMapping();
        services.AddScoped<IdempotencyEndpointFilter>();
        services.AddAuthentication();
        services.AddAuthorization();
        return services;
    }
}
