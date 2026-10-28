using ErrorOr;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Commands;

internal static class ResourceGroupCommandSupport
{
    public static ErrorOr<Success> Validate(
        ComponentAggregate component,
        ProjectAggregate project,
        string? region,
        IReadOnlyDictionary<Guid, string> forcedNames,
        ICatalogProvider catalogProvider)
    {
        if (!string.IsNullOrWhiteSpace(region))
        {
            var catalog = catalogProvider.GetVersion(project.CatalogVersion);
            if (catalog is null || !catalog.Regions.Any(candidate =>
                    string.Equals(candidate.Name, region.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Validation("RESOURCE_GROUP_REGION_INVALID", "The selected region is not present in the project's pinned catalog.");
            }
        }

        if (forcedNames.Count == 0)
        {
            return Result.Success;
        }

        if (component.Mode == ComponentDeploymentMode.Single)
        {
            return Error.Validation("RESOURCE_GROUP_FORCED_NAME_TARGET_INVALID", "Forced resource group names can only target project environments.");
        }

        var projectEnvironmentIds = project.Environments.Select(environment => environment.Id).ToHashSet();
        var targetEnvironmentIds = component.TargetsAllEnvironments
            ? projectEnvironmentIds
            : component.TargetEnvironmentIds.ToHashSet();
        if (forcedNames.Keys.Any(environmentId =>
                !projectEnvironmentIds.Contains(environmentId) || !targetEnvironmentIds.Contains(environmentId)))
        {
            return Error.Validation("RESOURCE_GROUP_FORCED_NAME_TARGET_INVALID", "A forced name must target an environment selected by the component.");
        }

        return Result.Success;
    }
}
