using InfraFlowSculptor.Application.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraFlowSculptor.Emitters.InstallKit;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInstallKitEmitter(this IServiceCollection services)
    {
        services.TryAddSingleton<AzureDevOpsInstallKitPlatformEmitter>();
        services.AddSingleton<IInstallKitPlatformEmitter>(provider =>
            provider.GetRequiredService<AzureDevOpsInstallKitPlatformEmitter>());
        return services;
    }
}
