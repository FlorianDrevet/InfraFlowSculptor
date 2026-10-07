using InfraFlowSculptor.Application.Common.Events;
using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddJobHandlersFromAssembly(
        this IServiceCollection services,
        System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.DefinedTypes
                     .Where(type => !type.IsAbstract && !type.IsInterface)
                     .Select(type => type.AsType()))
        {
            if (typeof(IJobHandler).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IJobHandler), type);
            }

            if (typeof(IDomainEventHandler).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IDomainEventHandler), type);
            }
        }

        return services;
    }
}
