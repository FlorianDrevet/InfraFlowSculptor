using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetDeploymentPlan;

public sealed class GetDeploymentPlanQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    EngineFacade engineFacade)
    : IQueryHandler<GetDeploymentPlanQuery, ErrorOr<DeploymentPlan>>
{
    public async ValueTask<ErrorOr<DeploymentPlan>> Handle(
        GetDeploymentPlanQuery query,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(
            query.ProjectId,
            cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var snapshot = snapshotResult.Value;
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation(
                "PROJECT_CATALOG_VERSION_UNAVAILABLE",
                "The project's pinned catalog version is unavailable.");
        }

        var evaluation = engineFacade.Evaluate(snapshot, catalog);
        var components = evaluation.Plan.Components;
        if (!string.IsNullOrWhiteSpace(query.Component))
        {
            var component = components.SingleOrDefault(plan =>
                string.Equals(plan.ComponentCode, query.Component.Trim(), StringComparison.OrdinalIgnoreCase));
            if (component is null)
            {
                return Error.NotFound("DEPLOYMENT_PLAN_COMPONENT_NOT_FOUND", "The requested deployment component was not found.");
            }

            components = [component];
        }

        if (!string.IsNullOrWhiteSpace(query.Target))
        {
            var targetCode = query.Target.Trim();
            var matches = components
                .Select(component => component with
                {
                    Targets = component.Targets.Where(target =>
                        string.Equals(target.Target.Code, targetCode, StringComparison.OrdinalIgnoreCase)).ToArray()
                })
                .Where(component => component.Targets.Count > 0)
                .ToArray();
            if (matches.Length == 0)
            {
                return Error.NotFound("DEPLOYMENT_PLAN_TARGET_NOT_FOUND", "The requested deployment target was not found.");
            }

            components = matches;
        }

        return evaluation.Plan with { Components = components };
    }
}
