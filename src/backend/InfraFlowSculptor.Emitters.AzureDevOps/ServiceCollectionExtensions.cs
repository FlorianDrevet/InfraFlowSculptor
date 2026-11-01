using InfraFlowSculptor.Application.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraFlowSculptor.Emitters.AzureDevOps;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureDevOpsEmitters(this IServiceCollection services)
    {
        services.TryAddSingleton<AzureDevOpsPipelineEmitter>();
        services.AddSingleton<IPipelineEmitter>(provider => provider.GetRequiredService<AzureDevOpsPipelineEmitter>());
        services.TryAddSingleton<AzureDevOpsStepTranslator>();
        services.AddSingleton<IStepTranslator>(provider => provider.GetRequiredService<AzureDevOpsStepTranslator>());
        return services;
    }
}
