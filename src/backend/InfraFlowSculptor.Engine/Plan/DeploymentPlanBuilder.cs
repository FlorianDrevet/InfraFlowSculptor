using System.Globalization;
using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Exposure;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Tags;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Plan;

public sealed class DeploymentPlanBuilder(
    LinkKindRegistry linkKinds,
    ExposureResolver exposureResolver,
    DeploymentStrategyRegistry deploymentStrategies)
{
    private const string ReleaseSchema = "ifs-release/v1";
    private const string ContainerAppType = "ContainerApp";
    private const string LogAnalyticsWorkspaceType = "LogAnalyticsWorkspace";
    private const string DiagnosticSettingNamePrefix = "diag-";
    private const string LegacyDiagnosticLogCategoryGroup = "allLogs";
    private const string LegacyDiagnosticMetricCategory = "AllMetrics";
    private const string LegacyDiagnosticDestinationType = "Dedicated";
    private static readonly IReadOnlyDictionary<string, DiagnosticSettingDefaults> LegacyDiagnosticDefaultsByResourceType =
        new Dictionary<string, DiagnosticSettingDefaults>(StringComparer.Ordinal)
        {
            [ContainerAppType] = new(null, LegacyDiagnosticMetricCategory, LegacyDiagnosticDestinationType)
        };
    private const string ImageOutput = "image";
    private const string AzureSqlDnsSuffix = "database.windows.net";

    public DeploymentPlan Build(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        ComponentOrderingResult ordering,
        NamingPreviewResult naming)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(ordering);
        ArgumentNullException.ThrowIfNull(naming);

        var componentsById = snapshot.Components.ToDictionary(component => component.Id);
        var resourcesById = snapshot.Components
            .SelectMany(component => component.Resources.Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);
        var descriptors = catalog.ResourceTypes.ToDictionary(descriptor => descriptor.Type, StringComparer.OrdinalIgnoreCase);
        var componentOrder = ordering.OrderedComponents
            .Select((component, index) => (component.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index);

        var plans = ordering.OrderedComponents
            .Select(component => BuildComponent(
                snapshot,
                catalog,
                ordering.Wiring,
                naming,
                component,
                componentsById,
                resourcesById,
                descriptors,
                componentOrder))
            .ToArray();

        return new DeploymentPlan(
            new ProjectInfo(
                snapshot.Project.Name,
                snapshot.Project.Code,
                snapshot.Project.Description,
                snapshot.Project.InfrastructureLanguage,
                snapshot.Project.CiPlatform,
                snapshot.Project.ModuleSource,
                snapshot.Project.PublishingPreset),
            plans,
            CreatePins(catalog));
    }

    private ComponentPlan BuildComponent(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        WiringResult wiring,
        NamingPreviewResult naming,
        ComponentSnapshot component,
        Dictionary<Guid, ComponentSnapshot> componentsById,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        Dictionary<Guid, int> componentOrder)
    {
        var dependencies = wiring.Dependencies
            .Where(dependency => dependency.SourceComponentId == component.Id
                && dependency.TargetComponentId != component.Id
                && componentsById.ContainsKey(dependency.TargetComponentId))
            .Select(dependency => dependency.TargetComponentId)
            .Distinct()
            .OrderBy(id => componentOrder[id])
            .Select(id => componentsById[id].Code)
            .ToArray();

        var targets = TargetResolver.ResolveTargets(snapshot, component)
            .Select(target => BuildTarget(
                snapshot,
                catalog,
                wiring,
                naming,
                component,
                target,
                componentsById,
                resourcesById,
                descriptors,
                componentOrder))
            .ToArray();

        var applications = snapshot.Applications
            .Where(application => application.ComponentId == component.Id)
            .OrderBy(application => application.Name, StringComparer.Ordinal)
            .ThenBy(application => application.Id)
            .Select(application => BuildApplication(snapshot, application, component, targets))
            .ToArray();

        return new ComponentPlan(component.Code, Enum.Parse<ComponentDeploymentMode>(component.Mode), dependencies, targets, applications);
    }

    private TargetPlan BuildTarget(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        WiringResult wiring,
        NamingPreviewResult naming,
        ComponentSnapshot component,
        ResolvedTarget target,
        Dictionary<Guid, ComponentSnapshot> componentsById,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        Dictionary<Guid, int> componentOrder)
    {
        var resources = component.Resources
            .Select(resource => (Resource: resource, Target: TargetResolver.ResolveResourceTargets(snapshot, component, resource)
                .SingleOrDefault(candidate => candidate.Target.Id == target.Id)))
            .Where(entry => entry.Target is not null && entry.Target.IsPresent)
            .OrderBy(entry => entry.Resource.LogicalName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Resource.Type, StringComparer.Ordinal)
            .ToArray();
        var resourcePlans = resources
            .Select(entry => BuildResource(
                snapshot,
                wiring,
                naming,
                component,
                target,
                entry.Resource,
                entry.Target!,
                resourcesById,
                descriptors))
            .ToArray();

        var resourceGroupPlans = component.ResourceGroups
            .Select(group => (Group: group, Target: TargetResolver.ResolveResourceGroupTargets(snapshot, component, group)
                .SingleOrDefault(candidate => candidate.Target.Id == target.Id)))
            .Where(entry => entry.Target is not null && entry.Target.IsPresent)
            .OrderBy(entry => entry.Group.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Group.Id)
            .Select(entry =>
            {
                var groupName = GetResourceGroupName(naming, component, entry.Group, target.Id);
                return new ResourceGroupPlan(
                    entry.Group.Name,
                    groupName,
                    entry.Target!.Region,
                    entry.Target.IsPresent,
                    TagResolver.Resolve(snapshot, component, entry.Group, entry.Target));
            })
            .ToArray();

        var externalReferences = BuildExternalReferences(
            snapshot,
            wiring,
            component,
            target,
            resourcesById,
            descriptors,
            naming);
        var roleAssignments = BuildRoleAssignments(
            snapshot,
            naming,
            descriptors,
            wiring,
            component,
            target,
            resourcesById);
        var appSettings = BuildAppSettings(snapshot, wiring, component, target, naming, descriptors, resourcesById);
        var expectedSecrets = BuildExpectedSecrets(snapshot, component, target, resourcesById, naming);
        var dataAccess = BuildDataAccess(
            snapshot,
            wiring,
            component,
            target,
            resourcesById,
            descriptors,
            naming);
        var diagnosticSettings = BuildDiagnosticSettings(
            snapshot,
            wiring,
            component,
            target,
            resourcesById,
            descriptors,
            naming);
        var release = BuildReleaseData(
            snapshot,
            catalog,
            wiring,
            diagnosticSettings,
            roleAssignments,
            component,
            target,
            componentsById,
            resourcesById,
            descriptors,
            naming,
            dataAccess,
            componentOrder);

        var targetInfo = ResolveTargetInfo(snapshot, component, target);
        return new TargetPlan(
            targetInfo,
            resourceGroupPlans,
            resourcePlans,
            externalReferences,
            roleAssignments,
            appSettings,
            expectedSecrets,
            dataAccess,
            [],
            diagnosticSettings,
            release);
    }

    private DiagnosticSettingPlan[] BuildDiagnosticSettings(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        var plans = new List<DiagnosticSettingPlan>();
        foreach (var wired in wiring.Links
            .Where(link => link.Link.SourceComponentId == component.Id
                && linkKinds.Get(link.Link.Kind).ProducesDiagnosticSettings)
            .OrderBy(link => resourcesById.TryGetValue(link.Link.SourceResourceId, out var entry)
                ? entry.Resource.LogicalName : string.Empty, StringComparer.Ordinal)
            .ThenBy(link => resourcesById.TryGetValue(link.Link.SourceResourceId, out var entry)
                ? entry.Resource.Type : string.Empty, StringComparer.Ordinal))
        {
            if (!resourcesById.TryGetValue(wired.Link.SourceResourceId, out var source)
                || !descriptors.TryGetValue(source.Resource.Type, out var sourceDescriptor)
                || !sourceDescriptor.Definition.GetProperty("diagnostics").GetProperty("supported").GetBoolean()
                || !IsPresentForCode(snapshot, source.Component, source.Resource, target.Code)
                || !resourcesById.TryGetValue(wired.Link.TargetResourceId, out var destination)
                || !string.Equals(destination.Resource.Type, LogAnalyticsWorkspaceType, StringComparison.Ordinal))
            {
                continue;
            }

            var destinationTarget = ResolveTargetForComponent(snapshot, destination.Component, target.Code);
            if (destinationTarget is null
                || !IsPresentForCode(snapshot, destination.Component, destination.Resource, destinationTarget.Code))
            {
                continue;
            }

            var destinationName = destination.Resource.IsExisting
                ? ExtractName(destination.Resource.ExistingIds.GetValueOrDefault(destinationTarget.Id))
                : GetName(naming, destination.Resource.Id, destinationTarget.Id);
            var destinationGroup = destination.Component.ResourceGroups
                .Single(group => group.Id == destination.Resource.ResourceGroupId);
            var destinationResourceGroupName = GetResourceGroupName(
                naming,
                destination.Component,
                destinationGroup,
                destinationTarget.Id);
            var destinationResourceId = destination.Resource.IsExisting
                ? destination.Resource.ExistingIds.GetValueOrDefault(destinationTarget.Id)
                : BuildResourceIdForReference(
                    destination.Resource.Id,
                    destinationTarget.Code,
                    snapshot,
                    resourcesById,
                    descriptors,
                    naming);
            var destinationPlan = new DiagnosticDestinationPlan(
                destination.Component.Code,
                destinationTarget.Code,
                destination.Resource.Type,
                destination.Resource.LogicalName,
                destinationName,
                destinationResourceGroupName,
                destinationTarget.SubscriptionId,
                destinationResourceId,
                destination.Resource.IsExisting,
                wired.IsExternal);
            var diagnosticDefaults = sourceDescriptor.Definition.GetProperty("diagnostics");
            var defaults = diagnosticDefaults.TryGetProperty("settings", out var configuredSettings)
                && configuredSettings.ValueKind == JsonValueKind.Object
                    ? new DiagnosticSettingDefaults(
                        ReadNullableString(configuredSettings, "logCategoryGroup"),
                        ReadNullableString(configuredSettings, "metricCategory"),
                        ReadNullableString(configuredSettings, "destinationType") ?? string.Empty)
                    : LegacyDiagnosticDefaultsByResourceType.GetValueOrDefault(
                        source.Resource.Type,
                        new DiagnosticSettingDefaults(
                            LegacyDiagnosticLogCategoryGroup,
                            LegacyDiagnosticMetricCategory,
                            LegacyDiagnosticDestinationType));

            plans.Add(new DiagnosticSettingPlan(
                source.Resource.Type,
                source.Resource.LogicalName,
                GetName(naming, source.Resource.Id, target.Id),
                $"{DiagnosticSettingNamePrefix}{GetName(naming, source.Resource.Id, target.Id)}",
                destinationPlan,
                defaults.LogCategoryGroup,
                defaults.MetricCategory,
                defaults.DestinationType,
                []));
        }

        return plans
            .DistinctBy(plan => (plan.SourceResourceType, plan.SourceLogicalName, plan.Name))
            .OrderBy(plan => plan.SourceLogicalName, StringComparer.Ordinal)
            .ThenBy(plan => plan.SourceResourceType, StringComparer.Ordinal)
            .ThenBy(plan => plan.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private ResourcePlan BuildResource(
        ModelSnapshot snapshot,
        WiringResult wiring,
        NamingPreviewResult naming,
        ComponentSnapshot component,
        ResolvedTarget target,
        ResourceSnapshot resource,
        ResolvedResourceTarget resolvedTarget,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors)
    {
        if (!descriptors.TryGetValue(resource.Type, out var descriptor))
        {
            throw new InvalidOperationException($"The pinned catalog has no descriptor for resource type '{resource.Type}'.");
        }

        var name = GetName(naming, resource.Id, target.Id);
        var group = component.ResourceGroups.Single(candidate => candidate.Id == resource.ResourceGroupId);
        var groupName = GetResourceGroupName(naming, component, group, target.Id);
        var values = EffectiveValueResolver.Resolve(resource, target.Id, descriptor.Definition)
            .Select(value => new ResourcePropertyPlan(value.Name, Clone(value.Value)))
            .ToArray();
        var fixedValues = descriptor.Definition.GetProperty("fixedValues").EnumerateArray()
            .Select(value => new ResourcePropertyPlan(
                value.GetProperty("name").GetString()!,
                value.GetProperty("value").Clone()))
            .ToArray();
        var children = resource.Children
            .OrderBy(child => child.Name, StringComparer.Ordinal)
            .ThenBy(child => child.Type, StringComparer.Ordinal)
            .Select(child => BuildChild(child, descriptor, descriptors))
            .ToArray();
        var identities = BuildResourceIdentities(snapshot, wiring, resource, component, target, name, resourcesById, descriptors, naming);
        var exposure = exposureResolver.Resolve(resource, target.Id, descriptor) is { } resolvedExposure
            ? new ExposurePlan(resolvedExposure.Mode.ToString(),
                resolvedExposure is RestrictedExposure restricted ? restricted.Ranges.ToArray() : [])
            : null;
        var resourceTags = TagResolver.Resolve(snapshot, component, group, resource, resolvedTarget);
        var existingId = resource.ExistingIds.GetValueOrDefault(target.Id);
        var resourceId = resource.IsExisting
            ? existingId
            : BuildResourceId(snapshot, component, target, resource, name, groupName, resourcesById, descriptors, naming);
        var dependencies = BuildResourceDependencies(
            snapshot,
            wiring,
            resource,
            target,
            resourcesById,
            descriptors,
            naming);

        return new ResourcePlan(
            resource.Type,
            descriptor.Definition.GetProperty("armType").GetString()!,
            resource.LogicalName,
            resource.IsExisting ? ExtractName(existingId) : name,
            groupName,
            resolvedTarget.Region,
            resolvedTarget.IsPresent,
            resourceId,
            values,
            fixedValues,
            children,
            dependencies,
            identities,
            exposure,
            resourceTags,
            resource.IsExisting,
            existingId);
    }

    private ResourceDependencyPlan[] BuildResourceDependencies(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ResourceSnapshot resource,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        return wiring.Links
            .Where(wired => wired.Link.SourceResourceId == resource.Id
                && resourcesById.ContainsKey(wired.Link.TargetResourceId))
            .Select(wired =>
            {
                var entry = resourcesById[wired.Link.TargetResourceId];
                var dependencyTarget = ResolveTargetForComponent(snapshot, entry.Component, target.Code);
                if (dependencyTarget is null)
                {
                    return null;
                }

                var name = entry.Resource.IsExisting
                    ? ExtractName(entry.Resource.ExistingIds.GetValueOrDefault(dependencyTarget.Id))
                    : GetName(naming, entry.Resource.Id, dependencyTarget.Id);
                var group = entry.Component.ResourceGroups.Single(candidate => candidate.Id == entry.Resource.ResourceGroupId);
                var groupName = GetResourceGroupName(naming, entry.Component, group, dependencyTarget.Id);
                var resourceId = entry.Resource.IsExisting
                    ? entry.Resource.ExistingIds.GetValueOrDefault(dependencyTarget.Id)
                    : BuildResourceId(
                        snapshot,
                        entry.Component,
                        dependencyTarget,
                        entry.Resource,
                        name,
                        groupName,
                        resourcesById,
                        descriptors,
                        naming);

                return new ResourceDependencyPlan(
                    wired.Link.Kind.ToString(),
                    entry.Component.Code,
                    dependencyTarget.Code,
                    entry.Resource.Type,
                    entry.Resource.LogicalName,
                    name,
                    groupName,
                    dependencyTarget.SubscriptionId,
                    resourceId,
                    entry.Resource.IsExisting,
                    wired.IsExternal,
                    wired.CreatesDeploymentDependency
                    && (entry.Resource.IsExisting
                        || IsPresentForCode(snapshot, entry.Component, entry.Resource, dependencyTarget.Code)));
            })
            .Where(dependency => dependency is not null)
            .Select(dependency => dependency!)
            .DistinctBy(dependency => (dependency.Kind, dependency.ComponentCode, dependency.TargetCode,
                dependency.ResourceType, dependency.LogicalName, dependency.ResourceId))
            .OrderBy(dependency => dependency.Kind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.ComponentCode, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetCode, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.LogicalName, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.ResourceType, StringComparer.Ordinal)
            .ToArray();
    }

    private static ResourceChildPlan BuildChild(
        ResourceChildSnapshot child,
        CatalogResourceTypeDescriptor parentDescriptor,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors)
    {
        if (!descriptors.TryGetValue(child.Type, out var childDescriptor))
        {
            throw new InvalidOperationException($"The pinned catalog has no descriptor for child type '{child.Type}'.");
        }

        var declaredChild = parentDescriptor.Definition.GetProperty("children").EnumerateArray()
            .FirstOrDefault(definition => string.Equals(
                definition.GetProperty("type").GetString(), child.Type, StringComparison.Ordinal));
        if (declaredChild.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Resource type '{parentDescriptor.Type}' does not declare child type '{child.Type}'.");
        }

        var properties = childDescriptor.Definition.GetProperty("properties").EnumerateArray()
            .Select(definition =>
            {
                var propertyName = definition.GetProperty("name").GetString()!;
                JsonElement? value = child.Properties.TryGetValue(propertyName, out var storedValue)
                    ? storedValue.Clone()
                    : definition.TryGetProperty("default", out var defaultValue) ? defaultValue.Clone() : null;
                return new ResourcePropertyPlan(propertyName, value);
            })
            .ToArray();
        return new ResourceChildPlan(
            child.Type,
            childDescriptor.Definition.GetProperty("armType").GetString()!,
            child.Name,
            properties);
    }

    private ResourceIdentityPlan[] BuildResourceIdentities(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ResourceSnapshot resource,
        ComponentSnapshot component,
        ResolvedTarget target,
        string? name,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        var identities = new List<ResourceIdentityPlan>();
        foreach (var identity in wiring.Identities
                     .Where(identity => identity.ComponentId == component.Id && identity.ResourceId == resource.Id)
                     .OrderBy(identity => identity.Kind)
                     .ThenBy(identity => identity.AttachedIdentityResourceId))
        {
            var attached = identity.AttachedIdentityResourceId is { } attachedId
                && resourcesById.TryGetValue(attachedId, out var attachedEntry)
                    ? GetName(naming, attachedEntry.Resource.Id, target.Id)
                    : null;
            identities.Add(new ResourceIdentityPlan(
                identity.Kind == LinkIdentityKind.System ? "systemAssigned" : "userAssigned",
                attached ?? name ?? resource.LogicalName,
                identity.AttachedIdentityResourceId is { } id
                    ? BuildResourceIdForReference(id, target.Code, snapshot, resourcesById, descriptors, naming)
                    : null,
                Attached: identity.Kind == LinkIdentityKind.System || identity.AttachedIdentityResourceId is not null));
        }

        if (resource.DefaultIdentityResourceId is { } defaultIdentityId
            && resourcesById.TryGetValue(defaultIdentityId, out var defaultIdentity)
            && identities.All(identity => !string.Equals(
                identity.ResourceId,
                BuildResourceIdForReference(defaultIdentityId, target.Code, snapshot, resourcesById, descriptors, naming),
                StringComparison.Ordinal)))
        {
            identities.Add(new ResourceIdentityPlan(
                "userAssigned",
                GetName(naming, defaultIdentityId, target.Id) is { Length: > 0 } identityName
                    ? identityName
                    : defaultIdentity.Resource.LogicalName,
                BuildResourceIdForReference(defaultIdentityId, target.Code, snapshot, resourcesById, descriptors, naming),
                Attached: true));
        }

        return identities
            .DistinctBy(identity => (identity.Kind, identity.ResourceId, identity.ResourceName))
            .ToArray();

    }

    private ExternalReference[] BuildExternalReferences(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        var refs = new List<ExternalReference>();
        var addedResourceIds = new HashSet<Guid>();

        void AddReference(Guid targetResourceId)
        {
            if (!addedResourceIds.Add(targetResourceId) || !resourcesById.TryGetValue(targetResourceId, out var entry))
            {
                return;
            }

            var externalTarget = ResolveTargetForComponent(snapshot, entry.Component, target.Code);
            if (externalTarget is null)
            {
                return;
            }

            var name = entry.Resource.IsExisting
                ? ExtractName(entry.Resource.ExistingIds.GetValueOrDefault(externalTarget.Id))
                : GetName(naming, entry.Resource.Id, externalTarget.Id);
            var group = entry.Component.ResourceGroups.Single(candidate => candidate.Id == entry.Resource.ResourceGroupId);
            var groupName = GetResourceGroupName(naming, entry.Component, group, externalTarget.Id);
            var resolvedResourceId = entry.Resource.IsExisting
                ? entry.Resource.ExistingIds.GetValueOrDefault(externalTarget.Id)
                : BuildResourceId(snapshot, entry.Component, externalTarget, entry.Resource, name, groupName,
                    resourcesById, descriptors, naming);
            refs.Add(new ExternalReference(
                entry.Component.Code,
                externalTarget.Code,
                entry.Resource.Type,
                entry.Resource.LogicalName,
                name,
                groupName,
                externalTarget.SubscriptionId,
                resolvedResourceId,
                entry.Resource.IsExisting));

            if (!descriptors.TryGetValue(entry.Resource.Type, out var descriptor)
                || !descriptor.Definition.GetProperty("generation").GetProperty("Bicep")
                    .TryGetProperty("existing", out var existing)
                || !existing.TryGetProperty("parentType", out var parentTypeElement))
            {
                return;
            }

            var parentType = parentTypeElement.GetString();
            var parentId = wiring.Links
                .Where(link => link.Link.SourceResourceId == entry.Resource.Id
                    && linkKinds.Get(link.Link.Kind).IsResourceHostingRelationship)
                .Select(link => link.Link.TargetResourceId)
                .FirstOrDefault(candidate => resourcesById.TryGetValue(candidate, out var parent)
                    && string.Equals(parent.Resource.Type, parentType, StringComparison.Ordinal));
            if (parentId != Guid.Empty)
            {
                AddReference(parentId);
            }
        }

        foreach (var wired in wiring.Links.Where(wired => wired.IsExternal && wired.Link.SourceComponentId == component.Id))
        {
            AddReference(wired.Link.TargetResourceId);
        }

        return refs
            .DistinctBy(reference => (reference.ComponentCode, reference.TargetCode, reference.ResourceType, reference.LogicalName, reference.ResourceId))
            .OrderBy(reference => reference.ComponentCode, StringComparer.Ordinal)
            .ThenBy(reference => reference.TargetCode, StringComparer.Ordinal)
            .ThenBy(reference => reference.LogicalName, StringComparer.Ordinal)
            .ThenBy(reference => reference.ResourceType, StringComparer.Ordinal)
            .ToArray();
    }

    private RoleAssignmentPlan[] BuildRoleAssignments(
        ModelSnapshot snapshot,
        NamingPreviewResult naming,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById)
    {
        return wiring.RoleAssignments
            .Where(assignment => assignment.OwnerComponentId == component.Id)
            .Select(assignment =>
            {
                var presentOrigins = assignment.Origins
                    .Where(origin => IsPresentForCode(snapshot, origin.SourceResourceId, target.Code, resourcesById)
                        && IsPresentForCode(snapshot, origin.TargetResourceId, target.Code, resourcesById))
                    .ToArray();
                return (Assignment: assignment, Origins: presentOrigins);
            })
            .Where(entry => entry.Origins.Length > 0
                && IsPresentForCode(snapshot, entry.Assignment.PrincipalResourceId, target.Code, resourcesById)
                && IsPresentForCode(snapshot, entry.Assignment.ScopeResourceId, target.Code, resourcesById))
            .Select(entry =>
            {
                var assignment = entry.Assignment;
                var principalId = BuildArmReference(snapshot, assignment.PrincipalResourceId, target.Code, resourcesById, descriptors, naming);
                var scopeId = BuildArmReference(snapshot, assignment.ScopeResourceId, target.Code, resourcesById, descriptors, naming);
                var origins = entry.Origins
                    .Select(origin => new RoleAssignmentOriginPlan(
                        origin.LinkOrigin == LinkOrigin.Implicit ? "implicit" : "explicit",
                        ToCamelCase(origin.LinkKind.ToString()),
                        GetResourceLabel(origin.SourceResourceId, resourcesById),
                        GetResourceLabel(origin.TargetResourceId, resourcesById)))
                    .OrderBy(origin => origin.Kind, StringComparer.Ordinal)
                    .ThenBy(origin => origin.LinkKind, StringComparer.Ordinal)
                    .ThenBy(origin => origin.Source, StringComparer.Ordinal)
                    .ThenBy(origin => origin.Target, StringComparer.Ordinal)
                    .ToArray();
                return new RoleAssignmentPlan(
                    assignment.PrincipalKind == RolePrincipalKind.SystemAssigned ? "systemAssigned" : "userAssigned",
                    principalId,
                    assignment.RoleName,
                    assignment.ScopeKind == LinkAccessScope.ResourceGroup ? "resourceGroup" : "resource",
                    scopeId,
                    assignment.DeterministicName,
                    origins);
            })
            .OrderBy(assignment => assignment.Principal, StringComparer.Ordinal)
            .ThenBy(assignment => assignment.Role, StringComparer.Ordinal)
            .ThenBy(assignment => assignment.Scope, StringComparer.Ordinal)
            .ThenBy(assignment => assignment.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private AppSettingPlan[] BuildAppSettings(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        NamingPreviewResult naming,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById)
    {
        var explicitSettings = snapshot.AppSettings
            .Where(setting => setting.ComponentId == component.Id)
            .Where(setting => resourcesById.TryGetValue(setting.ApplicationResourceId, out var app)
                && app.Component.Id == component.Id
                && IsPresentForCode(snapshot, app.Component, app.Resource, target.Code)
                && (setting.SourceResourceId is not { } sourceId
                    || IsPresentForCode(snapshot, sourceId, target.Code, resourcesById))
                && (setting.KeyVaultResourceId is not { } keyVaultId
                    || IsPresentForCode(snapshot, keyVaultId, target.Code, resourcesById))
                && (setting.IdentityResourceId is not { } identityId
                    || IsPresentForCode(snapshot, identityId, target.Code, resourcesById)))
            .Select(setting =>
            {
                var literalValue = setting.SourceKind == AppSettingSourceKind.Literal
                    ? setting.LiteralOverrides.TryGetValue(target.Id, out var overrideValue) ? overrideValue : setting.LiteralValue
                    : null;
                var vaultName = setting.KeyVaultResourceId is { } vaultId && resourcesById.TryGetValue(vaultId, out var vault)
                    ? GetName(naming, vault.Resource.Id, FindTargetId(snapshot, vault.Component, target.Code) ?? target.Id)
                    : null;
                return new AppSettingPlan(
                    setting.Name,
                    "environmentVariable",
                    ToCamelCase(setting.SourceKind.ToString()),
                    literalValue,
                    setting.SourceResourceId is { } sourceId && resourcesById.TryGetValue(sourceId, out var source)
                        ? source.Resource.LogicalName
                        : null,
                    setting.OutputName,
                    vaultName,
                    setting.SecretName,
                    setting.IdentityResourceId is { } identityId
                        ? BuildResourceIdForReference(identityId, target.Code, snapshot, resourcesById, descriptors, naming)
                        : null,
                    setting.SecretFeedKind is { } feed ? ToCamelCase(feed.ToString()) : null,
                    setting.PipelineVariableName,
                    IsImplicit: false,
                    IsSensitive: setting.SourceKind == AppSettingSourceKind.KeyVaultSecret);
            });

        var implicitSettings = wiring.ImplicitApplicationSettings
            .Where(setting => resourcesById.TryGetValue(setting.ApplicationResourceId, out var app)
                && app.Component.Id == component.Id
                && IsPresentForCode(snapshot, app.Component, app.Resource, target.Code))
            .Select(setting => new AppSettingPlan(
                setting.Name,
                "environmentVariable",
                "resourceOutput",
                setting.IsSensitive ? null : setting.Value,
                setting.SourceResourceId is { } sourceId && resourcesById.TryGetValue(sourceId, out var source)
                    ? source.Resource.LogicalName
                    : null,
                setting.OutputName,
                null,
                null,
                null,
                null,
                null,
                IsImplicit: true,
                setting.IsSensitive));

        return explicitSettings.Concat(implicitSettings)
            .OrderBy(setting => setting.Name, StringComparer.Ordinal)
            .ThenBy(setting => setting.IsImplicit)
            .ToArray();
    }

    private static PipelineSecretPlan[] BuildExpectedSecrets(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        NamingPreviewResult naming)
    {
        return snapshot.AppSettings
            .Where(setting => setting.ComponentId == component.Id
                && setting.SourceKind == AppSettingSourceKind.KeyVaultSecret
                && setting.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                && setting.KeyVaultResourceId is not null
                && setting.PipelineVariableName is not null
                && resourcesById.TryGetValue(setting.KeyVaultResourceId.Value, out var vault)
                && vault.Component.Id != component.Id
                && resourcesById.TryGetValue(setting.ApplicationResourceId, out var app)
                && IsPresentForCode(snapshot, app.Component, app.Resource, target.Code)
                && IsPresentForCode(snapshot, vault.Component, vault.Resource, target.Code))
            .Select(setting =>
            {
                var vault = resourcesById[setting.KeyVaultResourceId!.Value];
                return new PipelineSecretPlan(
                    setting.PipelineVariableName!,
                    GetName(naming, vault.Resource.Id, FindTargetId(snapshot, vault.Component, target.Code) ?? target.Id) is { Length: > 0 } vaultName
                        ? vaultName
                        : vault.Resource.LogicalName,
                    setting.SecretName ?? string.Empty,
                    vault.Component.Code);
            })
            .DistinctBy(secret => (secret.Variable, secret.Vault, secret.Secret, secret.Writer))
            .OrderBy(secret => secret.Variable, StringComparer.Ordinal)
            .ToArray();
    }

    private DataAccessPlan[] BuildDataAccess(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        var result = new List<(DataAccessPlan Plan, DataAccessLevel Level)>();
        foreach (var request in wiring.DataAccessRequests.Where(request => request.OwnerComponentId == component.Id))
        {
            if (!resourcesById.TryGetValue(request.SourceResourceId, out var sourceEntry)
                || !resourcesById.TryGetValue(request.TargetResourceId, out var databaseEntry))
            {
                continue;
            }

            var sourceTarget = ResolveResourceTargetForCode(snapshot, sourceEntry.Component, sourceEntry.Resource, target.Code);
            var databaseTarget = ResolveResourceTargetForCode(snapshot, databaseEntry.Component, databaseEntry.Resource, target.Code);
            if (sourceTarget is null || databaseTarget is null || !sourceTarget.IsPresent || !databaseTarget.IsPresent)
            {
                continue;
            }

            var serverEntry = FindHostingParent(wiring, databaseEntry, resourcesById) ?? databaseEntry;
            var serverTarget = ResolveResourceTargetForCode(snapshot, serverEntry.Component, serverEntry.Resource, databaseTarget.Target.Code);
            if (serverTarget is null || !serverTarget.IsPresent)
            {
                continue;
            }

            var serverDescriptor = descriptors[serverEntry.Resource.Type];
            var serverName = GetName(naming, serverEntry.Resource.Id, serverTarget.Target.Id);
        var serverSuffix = serverDescriptor.Definition.GetProperty("naming").GetProperty("availabilityDomain").GetString()
                ?? AzureSqlDnsSuffix;
            var principalResource = request.IdentityKind == LinkIdentityKind.UserAssigned && request.IdentityResourceId is { } userIdentityId
                ? userIdentityId
                : request.SourceResourceId;
            var principalId = BuildResourceIdForReference(
                principalResource,
                sourceTarget.Target.Code,
                snapshot,
                resourcesById,
                descriptors,
                naming) ?? string.Empty;
            var exposure = exposureResolver.Resolve(serverEntry.Resource, serverTarget.Target.Id, serverDescriptor)?.Mode.ToString() ?? "Public";

            var plan = new DataAccessPlan(
                $"{serverName}.{serverSuffix}",
                GetName(naming, databaseEntry.Resource.Id, databaseTarget.Target.Id) is { Length: > 0 } databaseName
                    ? databaseName
                    : databaseEntry.Resource.LogicalName,
                request.IdentityKind == LinkIdentityKind.System ? "systemAssigned" : "userAssigned",
                principalId,
                request.Level switch
                {
                    DataAccessLevel.Lecture => "ReadOnly",
                    DataAccessLevel.LectureEcriture => "ReadWrite",
                    DataAccessLevel.Schema => "Schema",
                    _ => throw new InvalidOperationException($"Data access level '{request.Level}' is unsupported.")
                },
                request.DatabaseRoles.Order(StringComparer.Ordinal).ToArray(),
                exposure,
                request.ApplyScript,
                request.RemoveScript);
            result.Add((plan, request.Level));
        }

        return result
            .GroupBy(entry => (entry.Plan.Server, entry.Plan.Database, entry.Plan.PrincipalResourceId))
            .Select(group =>
            {
                var entries = group.ToArray();
                var strongest = entries
                    .OrderByDescending(entry => GetDataAccessLevelRank(entry.Level))
                    .First()
                    .Plan;
                return strongest with
                {
                    DatabaseRoles = entries.SelectMany(entry => entry.Plan.DatabaseRoles)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .OrderBy(access => access.Server, StringComparer.Ordinal)
            .ThenBy(access => access.Database, StringComparer.Ordinal)
            .ThenBy(access => access.PrincipalResourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetDataAccessLevelRank(DataAccessLevel level) => level switch
    {
        DataAccessLevel.Lecture => 0,
        DataAccessLevel.LectureEcriture => 1,
        DataAccessLevel.Schema => 2,
        _ => throw new InvalidOperationException($"Data access level '{level}' is unsupported.")
    };

    private ReleaseDataPlan BuildReleaseData(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        WiringResult wiring,
        DiagnosticSettingPlan[] diagnosticSettings,
        RoleAssignmentPlan[] roleAssignments,
        ComponentSnapshot component,
        ResolvedTarget target,
        Dictionary<Guid, ComponentSnapshot> componentsById,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming,
        IReadOnlyList<DataAccessPlan> dataAccess,
        Dictionary<Guid, int> componentOrder)
    {
        var releaseDependencies = BuildReleaseDependencies(
            snapshot, wiring, component, target, componentsById, resourcesById, descriptors, naming, componentOrder);
        var secretWrites = snapshot.AppSettings
            .Where(setting => setting.SourceKind == AppSettingSourceKind.KeyVaultSecret
                && setting.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                && setting.KeyVaultResourceId is { } vaultId
                && resourcesById.TryGetValue(vaultId, out var vault)
                && vault.Component.Id == component.Id
                && IsPresentForCode(snapshot, vault.Component, vault.Resource, target.Code))
            .Select(setting =>
            {
                var vault = resourcesById[setting.KeyVaultResourceId!.Value];
                return new ReleaseSecretWritePlan(
                    setting.PipelineVariableName ?? string.Empty,
                    GetName(naming, vault.Resource.Id, FindTargetId(snapshot, vault.Component, target.Code) ?? target.Id) is { Length: > 0 } vaultName
                        ? vaultName
                        : vault.Resource.LogicalName,
                    setting.SecretName ?? string.Empty,
                    "pipeline");
            })
            .DistinctBy(secret => (secret.Variable, secret.Vault, secret.Secret))
            .OrderBy(secret => secret.Variable, StringComparer.Ordinal)
            .ToArray();
        var secretReferences = snapshot.AppSettings
            .Where(setting => setting.ComponentId == component.Id
                && setting.SourceKind == AppSettingSourceKind.KeyVaultSecret
                && setting.KeyVaultResourceId is not null
                && resourcesById.TryGetValue(setting.KeyVaultResourceId.Value, out var vault)
                && vault.Component.Id != component.Id
                && resourcesById.TryGetValue(setting.ApplicationResourceId, out var app)
                && IsPresentForCode(snapshot, app.Component, app.Resource, target.Code)
                && IsPresentForCode(snapshot, vault.Component, vault.Resource, target.Code))
            .Select(setting =>
            {
                var vault = resourcesById[setting.KeyVaultResourceId!.Value];
                var vaultName = GetName(naming, vault.Resource.Id, FindTargetId(snapshot, vault.Component, target.Code) ?? target.Id);
                return new ReleaseSecretReferencePlan(
                    vaultName.Length > 0 ? vaultName : vault.Resource.LogicalName,
                    setting.SecretName ?? string.Empty,
                    vault.Component.Code);
            })
            .DistinctBy(secret => (secret.Vault, secret.Secret, secret.Writer))
            .OrderBy(secret => secret.Vault, StringComparer.Ordinal)
            .ThenBy(secret => secret.Secret, StringComparer.Ordinal)
            .ToArray();

        var appOwnedState = new List<ReleaseAppOwnedStatePlan>();
        var restartOnSecretChange = new List<string>();
        foreach (var application in snapshot.Applications.Where(application => application.ComponentId == component.Id))
        {
            if (!resourcesById.TryGetValue(application.ResourceId, out var entry)
                || !string.Equals(entry.Resource.Type, ContainerAppType, StringComparison.Ordinal)
                || !IsPresentForCode(snapshot, entry.Component, entry.Resource, target.Code)
                || string.IsNullOrWhiteSpace(BuildResourceIdForReference(
                    application.ResourceId, target.Code, snapshot, resourcesById, descriptors, naming)))
            {
                continue;
            }

            var applicationResourceId = BuildResourceIdForReference(
                application.ResourceId, target.Code, snapshot, resourcesById, descriptors, naming)!;

            appOwnedState.Add(new ReleaseAppOwnedStatePlan(
                applicationResourceId,
                $"{CreateResourceIdentifier(entry.Resource.Type, entry.Resource.LogicalName, descriptors)}.{ImageOutput}",
                "containerAppImage",
                catalog.Pins.StartupImage.Reference));

            if (snapshot.AppSettings.Any(setting => setting.ApplicationResourceId == application.ResourceId
                && setting.SourceKind == AppSettingSourceKind.KeyVaultSecret))
            {
                restartOnSecretChange.Add(applicationResourceId);
            }
        }

        var accessObjectTypes = new SortedSet<string>(StringComparer.Ordinal);
        if (roleAssignments.Length > 0)
        {
            accessObjectTypes.Add("Microsoft.Authorization/roleAssignments");
        }

        if (diagnosticSettings.Length > 0)
        {
            accessObjectTypes.Add("Microsoft.Insights/diagnosticSettings");
        }

        return new ReleaseDataPlan(
            ReleaseSchema,
            snapshot.Project.Code,
            component.Code,
            target.Code,
            $"ifs-{snapshot.Project.Code}-{component.Code}-{target.Code}",
            target.SubscriptionId,
            target.Region,
            ResolveTargetInfo(snapshot, component, target).IsProtected,
            null,
            releaseDependencies,
            secretWrites,
            secretReferences,
            appOwnedState.OrderBy(state => state.ResourceId, StringComparer.Ordinal).ToArray(),
            dataAccess.Select(access => new ReleaseDataAccessPlan(
                    access.Server,
                    access.Database,
                    new ReleaseDataAccessPrincipalPlan(access.PrincipalResourceId, access.PrincipalKind),
                    access.Level,
                    access.Exposure))
                .OrderBy(access => access.Server, StringComparer.Ordinal)
                .ThenBy(access => access.Database, StringComparer.Ordinal)
                .ToArray(),
            accessObjectTypes.ToArray(),
            restartOnSecretChange.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private List<ReleaseDependencyPlan> BuildReleaseDependencies(
        ModelSnapshot snapshot,
        WiringResult wiring,
        ComponentSnapshot component,
        ResolvedTarget target,
        Dictionary<Guid, ComponentSnapshot> componentsById,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming,
        Dictionary<Guid, int> componentOrder)
    {
        var linkedComponents = wiring.Dependencies
            .Where(dependency => dependency.SourceComponentId == component.Id && dependency.TargetComponentId != component.Id)
            .Select(dependency => dependency.TargetComponentId)
            .Distinct()
            .OrderBy(id => componentOrder.GetValueOrDefault(id, int.MaxValue));
        var plans = new List<ReleaseDependencyPlan>();
        foreach (var dependencyId in linkedComponents)
        {
            var referenced = new List<string>();
            var addedResources = new HashSet<Guid>();
            var addedIds = new HashSet<string>(StringComparer.Ordinal);
            var dependencyLinks = wiring.Links
                .Where(wired => wired.Link.SourceComponentId == component.Id
                    && wired.Link.TargetComponentId == dependencyId
                    && wired.CreatesDeploymentDependency)
                .OrderBy(wired => wired.Link.Kind)
                .ThenBy(wired => resourcesById.GetValueOrDefault(wired.Link.TargetResourceId).Resource?.LogicalName, StringComparer.Ordinal)
                .ThenBy(wired => resourcesById.GetValueOrDefault(wired.Link.TargetResourceId).Resource?.Type, StringComparer.Ordinal);
            foreach (var wired in dependencyLinks)
            {
                if (resourcesById.TryGetValue(wired.Link.TargetResourceId, out var targetResource))
                {
                    var parent = FindHostingParent(wiring, targetResource, resourcesById);
                    if (parent is { } hostingParent)
                    {
                        AddReferencedResource(hostingParent);
                    }

                    AddReferencedResource(targetResource);
                }
            }

            var ids = referenced.Select(id => new ReleaseDependencyResourcePlan(id)).ToArray();
            if (ids.Length > 0)
            {
                plans.Add(new ReleaseDependencyPlan(componentsById[dependencyId].Code, ids));
            }

            void AddReferencedResource((ComponentSnapshot Component, ResourceSnapshot Resource) reference)
            {
                if (!addedResources.Add(reference.Resource.Id))
                {
                    return;
                }

                var referenceId = BuildResourceIdForReference(
                    reference.Resource.Id, target.Code, snapshot, resourcesById, descriptors, naming);
                if (referenceId is null || !addedIds.Add(referenceId))
                {
                    return;
                }

                referenced.Add(referenceId);
                if (!descriptors.TryGetValue(reference.Resource.Type, out var parentDescriptor))
                {
                    return;
                }

                foreach (var child in reference.Resource.Children
                             .OrderBy(child => child.Name, StringComparer.Ordinal)
                             .ThenBy(child => child.Type, StringComparer.Ordinal))
                {
                    if (!descriptors.TryGetValue(child.Type, out var childDescriptor))
                    {
                        continue;
                    }

                    var parentId = BuildResourceIdForReference(
                        reference.Resource.Id, target.Code, snapshot, resourcesById, descriptors, naming);
                    var nestedPath = GetNestedResourcePath(
                        parentDescriptor.Definition.GetProperty("armType").GetString()!,
                        childDescriptor.Definition.GetProperty("armType").GetString()!);
                    if (parentId is not null && nestedPath is not null)
                    {
                        var childId = $"{parentId}/{nestedPath}/{child.Name}";
                        if (addedIds.Add(childId))
                        {
                            referenced.Add(childId);
                        }
                    }
                }
            }
        }

        return plans;
    }

    private ApplicationPlan BuildApplication(
        ModelSnapshot snapshot,
        ApplicationSnapshot application,
        ComponentSnapshot component,
        IReadOnlyList<TargetPlan> targetPlans)
    {
        var resource = component.Resources.Single(candidate => candidate.Id == application.ResourceId);
        var definition = new ApplicationDefinition(
            application.Name,
            application.CodeSourcePath,
            application.DockerfilePath,
            application.BuildContextPath,
            application.ImageRepository,
            application.HealthCheckPath,
            application.DeploymentStrategy,
            application.BuildRegistryEnvironmentId,
            application.Steps);
        var strategy = deploymentStrategies.Get(application.DeploymentStrategy).CreatePlan(definition);
        var imagePull = snapshot.Links.SingleOrDefault(link =>
            link.SourceResourceId == application.ResourceId
            && linkKinds.Get(link.Kind).IsImagePullRelationship);
        var registryComponent = imagePull is null
            ? null
            : snapshot.Components.SingleOrDefault(candidate => candidate.Id == imagePull.TargetComponentId);
        var targets = targetPlans.Select(target =>
        {
            var selectedRegistryTargetId = registryComponent is null
                || string.Equals(registryComponent.Mode, "Single", StringComparison.Ordinal)
                    ? null
                    : FindTargetId(snapshot, component, target.Target.Code);
            var registry = BuildRegistryResolver.Resolve(snapshot, application.ResourceId, linkKinds, selectedRegistryTargetId);
            return new ApplicationTargetPlan(
                target.Target.Code,
                registry.ComponentCode,
                registry.ResourceLogicalName,
                registry.RegistryName,
                registry.PromotionRequired,
                strategy.InfrastructurePrerequisites,
                strategy.StageSteps);
        }).ToArray();
        return new ApplicationPlan(
            application.Name,
            resource.LogicalName,
            application.CodeSourcePath,
            application.DockerfilePath,
            application.BuildContextPath,
            application.ImageRepository,
            application.HealthCheckPath,
            application.DeploymentStrategy,
            targets);
    }

    private static TargetInfo ResolveTargetInfo(ModelSnapshot snapshot, ComponentSnapshot component, ResolvedTarget target)
    {
        var single = component.SingleTarget is { } singleTarget && singleTarget.Id == target.Id ? singleTarget : null;
        var environment = snapshot.Environments.SingleOrDefault(candidate => candidate.Id == target.Id);
        return new TargetInfo(
            target.Code,
            target.SubscriptionId,
            target.Region,
            single?.DeploymentConnection ?? environment?.DeploymentConnection ?? string.Empty,
            single?.IsProtected ?? environment?.IsProtected ?? false,
            single?.Approvers ?? environment?.Approvers ?? [],
            single?.Runners ?? environment?.Runners ?? []);
    }

    private static CatalogPins CreatePins(CatalogVersion catalog) => new(
        catalog.Version,
        catalog.Fingerprint,
        catalog.Pins.BicepCliVersion,
        catalog.Pins.StartupImage,
        SortDictionary(catalog.Pins.Modules),
        SortDictionary(catalog.Pins.PowerShellModules),
        catalog.Pins.PipelineValidation,
        SortDictionary(catalog.Pins.NativeResources));

    private static Dictionary<string, TValue> SortDictionary<TValue>(IReadOnlyDictionary<string, TValue> values) =>
        values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private string? BuildResourceId(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResolvedTarget target,
        ResourceSnapshot resource,
        string? name,
        string? groupName,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        if (resource.IsExisting)
        {
            return resource.ExistingIds.GetValueOrDefault(target.Id);
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(groupName)
            || !descriptors.TryGetValue(resource.Type, out var descriptor))
        {
            return null;
        }

        var hostingParentId = snapshot.Links
            .Where(link => link.Origin == LinkOrigin.Explicit
                && link.SourceResourceId == resource.Id
                && linkKinds.Get(link.Kind).IsResourceHostingRelationship)
            .Select(link => link.TargetResourceId)
            .FirstOrDefault();
        if (hostingParentId != Guid.Empty && resourcesById.TryGetValue(hostingParentId, out var parent))
        {
            var parentTarget = ResolveTargetForComponent(snapshot, parent.Component, target.Code) ?? target;
            var parentName = GetName(naming, parent.Resource.Id, parentTarget.Id);
            var parentGroup = parent.Component.ResourceGroups.Single(group => group.Id == parent.Resource.ResourceGroupId);
            var parentGroupName = GetResourceGroupName(naming, parent.Component, parentGroup, parentTarget.Id);
            var parentId = BuildResourceId(snapshot, parent.Component, parentTarget, parent.Resource, parentName, parentGroupName,
                resourcesById, descriptors, naming);
            if (parentId is not null && descriptors.TryGetValue(parent.Resource.Type, out var parentDescriptor))
            {
                var childPath = GetNestedResourcePath(parentDescriptor.Definition.GetProperty("armType").GetString()!,
                    descriptor.Definition.GetProperty("armType").GetString()!);
                if (childPath is not null)
                {
                    return $"{parentId}/{childPath}/{name}";
                }
            }
        }

        return $"/subscriptions/{target.SubscriptionId:D}/resourceGroups/{groupName}/providers/{descriptor.Definition.GetProperty("armType").GetString()}/{name}";
    }

    private string? BuildResourceIdForReference(
        Guid resourceId,
        string targetCode,
        ModelSnapshot snapshot,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming)
    {
        if (!resourcesById.TryGetValue(resourceId, out var entry))
        {
            return null;
        }

        var target = ResolveTargetForComponent(snapshot, entry.Component, targetCode);
        if (target is null)
        {
            return null;
        }
        if (entry.Resource.IsExisting)
        {
            return entry.Resource.ExistingIds.GetValueOrDefault(target.Id);
        }

        var name = GetName(naming, resourceId, target.Id);
        var group = entry.Component.ResourceGroups.Single(candidate => candidate.Id == entry.Resource.ResourceGroupId);
        var groupName = GetResourceGroupName(naming, entry.Component, group, target.Id);
        return BuildResourceId(snapshot, entry.Component, target, entry.Resource, name, groupName, resourcesById, descriptors, naming);
    }

    private static string? GetNestedResourcePath(string parentArmType, string childArmType)
    {
        var parentSegments = parentArmType.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var childSegments = childArmType.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parentSegments.Length == 0 || childSegments.Length <= parentSegments.Length
            || !childSegments.Take(parentSegments.Length).SequenceEqual(parentSegments, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return string.Join('/', childSegments.Skip(parentSegments.Length));
    }

    private string BuildArmReference(
        ModelSnapshot snapshot,
        Guid id,
        string targetCode,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors,
        NamingPreviewResult naming) =>
        BuildResourceIdForReference(id, targetCode, snapshot, resourcesById, descriptors, naming) ?? string.Empty;

    private (ComponentSnapshot Component, ResourceSnapshot Resource)? FindHostingParent(
        WiringResult wiring,
        (ComponentSnapshot Component, ResourceSnapshot Resource) resource,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById)
    {
        var parentId = wiring.Links
            .Where(link => link.Link.SourceResourceId == resource.Resource.Id
                && linkKinds.Get(link.Link.Kind).IsResourceHostingRelationship)
            .Select(link => link.Link.TargetResourceId)
            .FirstOrDefault();
        return parentId != Guid.Empty && resourcesById.TryGetValue(parentId, out var parent) ? parent : null;
    }

    private static string GetResourceGroupName(
        NamingPreviewResult naming,
        ComponentSnapshot component,
        ResourceGroupSnapshot group,
        Guid targetId) =>
        naming.Items.FirstOrDefault(item => item.ComponentId == component.Id
            && item.ResourceGroupId == group.Id
            && item.TargetId == targetId)?.ResourceGroupName ?? string.Empty;

    private static string GetName(NamingPreviewResult naming, Guid resourceId, Guid targetId) =>
        naming.Items.FirstOrDefault(item => item.ResourceId == resourceId && item.TargetId == targetId)?.ResourceName ?? string.Empty;

    private static bool IsPresentForCode(ModelSnapshot snapshot, ComponentSnapshot component, ResourceSnapshot resource, string targetCode)
    {
        return ResolveResourceTargetForCode(snapshot, component, resource, targetCode)?.IsPresent == true;
    }

    private static bool IsPresentForCode(
        ModelSnapshot snapshot,
        Guid resourceId,
        string targetCode,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById) =>
        resourcesById.TryGetValue(resourceId, out var entry)
        && IsPresentForCode(snapshot, entry.Component, entry.Resource, targetCode);

    private static ResolvedResourceTarget? ResolveResourceTargetForCode(
        ModelSnapshot snapshot,
        ComponentSnapshot component,
        ResourceSnapshot resource,
        string targetCode)
    {
        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, resource);
        return targets.FirstOrDefault(target => string.Equals(target.Target.Code, targetCode, StringComparison.Ordinal))
            ?? (string.Equals(component.Mode, ComponentDeploymentMode.Single.ToString(), StringComparison.Ordinal)
                ? targets.SingleOrDefault()
                : null);
    }

    private static Guid? FindTargetId(ModelSnapshot snapshot, ComponentSnapshot component, string targetCode) =>
        ResolveTargetForComponent(snapshot, component, targetCode)?.Id;

    private static ResolvedTarget? ResolveTargetForComponent(ModelSnapshot snapshot, ComponentSnapshot component, string targetCode)
    {
        var targets = TargetResolver.ResolveTargets(snapshot, component);
        return targets.FirstOrDefault(target => string.Equals(target.Code, targetCode, StringComparison.Ordinal))
            ?? (string.Equals(component.Mode, ComponentDeploymentMode.Single.ToString(), StringComparison.Ordinal)
                ? targets.SingleOrDefault()
                : null);
    }

    private static string? ExtractName(string? resourceId) =>
        string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.TrimEnd('/').Split('/').LastOrDefault();

    private static JsonElement? Clone(JsonElement? value) => value?.Clone();

    private static string? ReadNullableString(JsonElement definition, string propertyName) =>
        definition.ValueKind == JsonValueKind.Object
        && definition.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.Null ? null : value.GetString()
            : null;

    private sealed record DiagnosticSettingDefaults(
        string? LogCategoryGroup,
        string? MetricCategory,
        string DestinationType);

    private static string ToCamelCase(string value) => value.Length == 0
        ? value
        : string.Concat(value[..1].ToLowerInvariant(), value[1..]);

    private static string GetResourceLabel(
        Guid resourceId,
        IReadOnlyDictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resourcesById) =>
        resourcesById.TryGetValue(resourceId, out var entry)
            ? $"{entry.Component.Code}/{entry.Resource.LogicalName}"
            : resourceId.ToString("D", CultureInfo.InvariantCulture);

    private static string CreateResourceIdentifier(
        string type,
        string logicalName,
        IReadOnlyDictionary<string, CatalogResourceTypeDescriptor> descriptors)
    {
        if (!descriptors.TryGetValue(type, out var descriptor))
        {
            return logicalName;
        }

        var words = logicalName.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var suffix = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        return $"{descriptor.Abbreviation}{suffix}";
    }

}
