using InfraFlowSculptor.Application.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraFlowSculptor.Emitters.Bicep;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBicepEmitter(this IServiceCollection services)
    {
        services.TryAddSingleton<PublicAvmModuleSourceStrategy>();
        services.AddSingleton<IModuleSourceStrategy>(provider =>
            provider.GetRequiredService<PublicAvmModuleSourceStrategy>());
        services.TryAddSingleton<BicepInfrastructureEmitter>();
        services.AddSingleton<IInfrastructureEmitter>(provider =>
            provider.GetRequiredService<BicepInfrastructureEmitter>());
        services.TryAddSingleton<BicepOutputChecker>();
        services.AddSingleton<IOutputChecker>(provider =>
            provider.GetRequiredService<BicepOutputChecker>());
        return services;
    }
}
