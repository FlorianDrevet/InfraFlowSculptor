using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraFlowSculptor.Worker;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerSecurityContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IVerifiedEmailResolver, VerifiedEmailResolver>();
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.TryAddScoped<IOrganizationRoleAuthorizer, EfOrganizationRoleAuthorizer>();
        services.TryAddScoped<IPlanLimitGuard, UnlimitedPlanLimitGuard>();
        return services;
    }
}
