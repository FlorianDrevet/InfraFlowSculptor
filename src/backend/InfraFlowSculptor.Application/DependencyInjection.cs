using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Naming.Availability;
using InfraFlowSculptor.Engine.Exposure;
using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(AuthorizationBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(UnitOfWorkBehavior<,>),
                typeof(ModelChangeBehavior<,>)
            ];
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.TryAddScoped<IJobDispatcher, OutboxJobDispatcher>();
        services.TryAddScoped<IModelCommandExecutor, ModelCommandExecutor>();
        services.TryAddSingleton<ModelCommandSerializer>();
        services.TryAddScoped<ModelSnapshotBuilder>();
        services.TryAddSingleton(_ => new ExposureHandlerRegistry(PilotExposureHandlers.Create()));
        services.TryAddSingleton<ExposureResolver>();
        services.TryAddSingleton(_ => new LinkKindRegistry(PilotLinkKindHandlers.Create()));
        services.TryAddSingleton(_ => new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]));
        services.TryAddSingleton<WiringEngine>();
        services.TryAddSingleton<ImpactAnalyzer>();
        services.TryAddScoped<NameAvailabilityService>();
        services.TryAddScoped<NameAvailabilityProcessor>();
        return services;
    }
}
