using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Engine.Applications;

public static class BuildRegistryResolver
{
    public static BuildRegistryResolution Resolve(
        ModelSnapshot snapshot,
        Guid applicationResourceId,
        LinkKindRegistry linkKinds,
        Guid? selectedEnvironmentId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(linkKinds);
        if (applicationResourceId == Guid.Empty)
        {
            throw new ArgumentException("An application resource id is required.", nameof(applicationResourceId));
        }

        var imagePullLinks = linkKinds.GetFromSource(snapshot, applicationResourceId, LinkKind.ImagePull);
        if (imagePullLinks.Count != 1)
        {
            return Unavailable();
        }

        var link = imagePullLinks[0];
        var source = snapshot.Components.SingleOrDefault(component => component.Id == link.SourceComponentId)
            ?.Resources.SingleOrDefault(resource => resource.Id == applicationResourceId);
        var registryComponent = snapshot.Components.SingleOrDefault(component => component.Id == link.TargetComponentId);
        var registry = registryComponent?.Resources.SingleOrDefault(resource => resource.Id == link.TargetResourceId);
        if (source?.Type != "ContainerApp" || registryComponent is null || registry?.Type != "ContainerRegistry")
        {
            return Unavailable();
        }

        var naming = NamingEngine.Preview(snapshot);

        var targetEnvironments = string.Equals(registryComponent.Mode, "Single", StringComparison.Ordinal)
            ? Array.Empty<EnvironmentSnapshot>()
            : snapshot.Environments
                .Where(environment => registryComponent.TargetsAllEnvironments
                    || registryComponent.TargetEnvironmentIds.Contains(environment.Id))
                .OrderBy(environment => environment.Position)
                .ToArray();

        if (string.Equals(registryComponent.Mode, "Single", StringComparison.Ordinal))
        {
            var targetId = registryComponent.SingleTarget?.Id;
            var item = targetId is null
                ? null
                : naming.Items.SingleOrDefault(candidate => candidate.ResourceId == registry.Id && candidate.TargetId == targetId);
            if (item is null || selectedEnvironmentId is not null)
            {
                return new BuildRegistryResolution(false, registryComponent.Code, registry.LogicalName, null, false, null, [], selectedEnvironmentId is null);
            }

            return new BuildRegistryResolution(
                true,
                registryComponent.Code,
                registry.LogicalName,
                item.ResourceName,
                false,
                null,
                [],
                true);
        }

        var options = targetEnvironments
            .Select(environment => (Environment: environment, Item: naming.Items.SingleOrDefault(candidate =>
                candidate.ResourceId == registry.Id && candidate.TargetId == environment.Id)))
            .Where(value => value.Item is not null)
            .Select(value => new BuildRegistryOption(value.Environment.Id, value.Environment.Code, value.Item!.ResourceName))
            .ToArray();
        var chosen = selectedEnvironmentId is null
            ? options.FirstOrDefault()
            : options.SingleOrDefault(option => option.EnvironmentId == selectedEnvironmentId.Value);
        if (chosen is null)
        {
            return new BuildRegistryResolution(false, registryComponent.Code, registry.LogicalName, null, options.Length > 1, null, options, false);
        }

        return new BuildRegistryResolution(
            true,
            registryComponent.Code,
            registry.LogicalName,
            chosen.RegistryName,
            options.Length > 1,
            chosen.EnvironmentId,
            options,
            true);
    }

    private static BuildRegistryResolution Unavailable(string? componentCode = null, string? resourceName = null) =>
        new(false, componentCode, resourceName, null, false, null, [], false);
}
