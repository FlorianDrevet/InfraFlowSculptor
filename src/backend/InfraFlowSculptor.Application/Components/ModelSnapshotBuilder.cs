using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Naming;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components;

public sealed class ModelSnapshotBuilder(IIfsDbContext dbContext, ICatalogProvider catalogProvider)
{
    public Task<ErrorOr<ModelSnapshot>> BuildAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        BuildAsync(projectId, includeOrganizationNamingReservations: false, cancellationToken);

    public Task<ErrorOr<ModelSnapshot>> BuildWithOrganizationNamingReservationsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        BuildAsync(projectId, includeOrganizationNamingReservations: true, cancellationToken);

    private async Task<ErrorOr<ModelSnapshot>> BuildAsync(
        Guid projectId,
        bool includeOrganizationNamingReservations,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .Include(candidate => candidate.NamingOverrides)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(projectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var components = await dbContext.Query<ComponentAggregate>()
            .Where(component => component.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var links = await dbContext.Query<LinkAggregate>()
            .Where(link => link.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var appSettings = await dbContext.Query<AppSettingAggregate>()
            .Where(setting => setting.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);
        var applications = await dbContext.Query<ApplicationAggregate>()
            .Where(application => application.ProjectId == new ProjectId(projectId))
            .ToArrayAsync(cancellationToken);

        var snapshot = CreateSnapshot(project, components, links, appSettings, applications);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        if (!includeOrganizationNamingReservations)
        {
            return snapshot.Value;
        }

        var otherProjects = await dbContext.Query<ProjectAggregate>()
            .Where(candidate => candidate.OrganizationId == project.OrganizationId
                && candidate.Id != project.Id
                && candidate.DeletedAt == null)
            .Include(candidate => candidate.Environments)
            .Include(candidate => candidate.NamingOverrides)
            .ToArrayAsync(cancellationToken);
        if (otherProjects.Length == 0)
        {
            return snapshot.Value;
        }

        var otherProjectIds = otherProjects.Select(candidate => candidate.Id).ToArray();
        var otherComponents = await dbContext.Query<ComponentAggregate>()
            .Where(component => otherProjectIds.Contains(component.ProjectId))
            .ToArrayAsync(cancellationToken);
        var reservations = new List<GlobalNameReservationSnapshot>();
        foreach (var otherProject in otherProjects)
        {
            var otherSnapshot = CreateSnapshot(
                otherProject,
                otherComponents.Where(component => component.ProjectId == otherProject.Id).ToArray(),
                [],
                [],
                []);
            if (otherSnapshot.IsError)
            {
                return otherSnapshot.Errors;
            }

            var otherPreview = NamingEngine.Preview(otherSnapshot.Value);
            if (otherPreview.Issues.Any(issue => issue.Code.StartsWith("NAMING_TEMPLATE_", StringComparison.Ordinal)
                || issue.Code is "NAMING_NAME_TOKEN_REQUIRED" or "NAMING_TOKEN_UNKNOWN"))
            {
                return Error.Validation(
                    "ORGANIZATION_NAMING_RESERVATIONS_UNAVAILABLE",
                    "Global name collisions cannot be checked while another project has an invalid naming template.");
            }

            reservations.AddRange(otherPreview.Items
                .Where(item => otherSnapshot.Value.NamingRules.TryGetValue(item.ResourceType, out var rule)
                    && string.Equals(rule.Uniqueness, "global", StringComparison.OrdinalIgnoreCase))
                .Select(item => new GlobalNameReservationSnapshot(
                    item.ResourceType,
                    item.IsExisting ? GetExistingResourceName(item.ResourceName) : item.ResourceName,
                    otherProject.Id.Value)));
        }

        return snapshot.Value with { OrganizationGlobalNames = reservations };
    }

    private static string GetExistingResourceName(string resourceId)
    {
        var normalizedId = resourceId.TrimEnd('/');
        var separatorIndex = normalizedId.LastIndexOf('/');
        return separatorIndex < 0 ? normalizedId : normalizedId[(separatorIndex + 1)..];
    }

    private ErrorOr<ModelSnapshot> CreateSnapshot(
        ProjectAggregate project,
        IReadOnlyList<ComponentAggregate> components,
        IReadOnlyList<LinkAggregate> links,
        IReadOnlyList<AppSettingAggregate> appSettings,
        IReadOnlyList<ApplicationAggregate> applications)
    {
        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var naming = new NamingConventionSnapshot(
            project.NamingPreset,
            project.DefaultNameTemplate,
            project.ResourceGroupNameTemplate)
        {
            TypeTemplates = project.NamingOverrides
                .Where(value => value.Template is not null)
                .ToDictionary(value => value.Type, value => value.Template!, StringComparer.OrdinalIgnoreCase),
            TypeAbbreviations = project.NamingOverrides
                .Where(value => value.Abbreviation is not null)
                .ToDictionary(value => value.Type, value => value.Abbreviation!, StringComparer.OrdinalIgnoreCase)
        };

        var environments = project.Environments.OrderBy(environment => environment.Position)
            .Select(environment => new EnvironmentSnapshot(environment.Code, environment.Region)
            {
                Id = environment.Id,
                Version = environment.Version,
                Name = environment.Name,
                Position = environment.Position,
                SubscriptionId = environment.SubscriptionId,
                IsProtected = environment.IsProtected,
                Approvers = environment.Approvers,
                Tags = environment.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase)
            })
            .ToArray();
        var componentSnapshots = components.Select(MapComponent).ToArray();
        var linkSnapshots = links.Select(link => new LinkSnapshot(
            link.Id,
            link.SourceComponentId,
            link.SourceResourceId,
            link.TargetComponentId,
            link.TargetResourceId,
            link.Kind,
            link.Origin,
            link.IdentityKind,
            link.IdentityResourceId,
            link.Roles.Select(role => role.Name).ToArray(),
            link.AccessScope,
            link.DataAccessLevel,
            link.Version)).ToArray();
        var appSettingSnapshots = appSettings.Select(setting => new AppSettingSnapshot(
            setting.Id,
            setting.ComponentId.Value,
            setting.ApplicationResourceId,
            setting.Name,
            setting.SourceKind,
            setting.LiteralSource?.Value,
            setting.LiteralSource?.Overrides.ToDictionary(value => value.TargetId, value => value.Value) ?? new Dictionary<Guid, string>(),
            setting.SourceResourceId,
            setting.OutputName,
            setting.KeyVaultResourceId,
            setting.SecretName,
            setting.SecretFeedKind,
            setting.PipelineVariableName,
            setting.IdentityResourceId) { Version = setting.Version }).ToArray();
        var applicationSnapshots = applications.Select(application => new ApplicationSnapshot(
            application.Id,
            application.ComponentId.Value,
            application.ResourceId,
            application.Name,
            application.CodeSourcePath,
            application.DockerfilePath,
            application.BuildContextPath,
            application.ImageRepository,
            application.HealthCheckPath,
            application.DeploymentStrategy,
            application.BuildRegistryEnvironmentId,
            application.Steps.ToArray()) { Version = application.Version }).ToArray();
        var snapshot = new ModelSnapshot(
            new ProjectSnapshot(project.Name, project.Code, project.Description)
            {
                Id = project.Id.Value,
                Version = project.Version,
                ModelVersion = project.ModelVersion,
                CatalogVersion = project.CatalogVersion,
                SystemTagsEnabled = project.SystemTagsEnabled,
                PublishingPreset = project.PublishingPreset
            },
            environments,
            naming,
            project.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            componentSnapshots)
        {
            NamingRules = CatalogNamingSnapshotMapper.CreateRules(catalog),
            RegionCodes = CatalogNamingSnapshotMapper.CreateRegionCodes(catalog),
            Links = linkSnapshots,
            AppSettings = appSettingSnapshots,
            Applications = applicationSnapshots
        };

        var objectVersions = new Dictionary<ObjectRef, int>
        {
            [new ObjectRef(ObjectType.Project, project.Id.Value)] = project.Version
        };
        foreach (var environment in environments)
        {
            objectVersions[new ObjectRef(ObjectType.Environment, environment.Id)] = environment.Version;
        }

        foreach (var component in componentSnapshots)
        {
            objectVersions[new ObjectRef(ObjectType.Component, component.Id)] = component.Version;
            foreach (var resourceGroup in component.ResourceGroups)
            {
                objectVersions[new ObjectRef(ObjectType.ResourceGroup, resourceGroup.Id)] = resourceGroup.Version;
            }

            foreach (var resource in component.Resources)
            {
                objectVersions[new ObjectRef(ObjectType.Resource, resource.Id)] = resource.Version;
            }
        }

        foreach (var link in linkSnapshots)
        {
            objectVersions[new ObjectRef(ObjectType.Link, link.Id)] = link.Version ?? 1;
        }

        foreach (var setting in appSettingSnapshots)
        {
            objectVersions[new ObjectRef(ObjectType.AppSetting, setting.Id)] = setting.Version;
        }

        foreach (var application in applicationSnapshots)
        {
            objectVersions[new ObjectRef(ObjectType.Application, application.Id)] = application.Version;
        }

        return snapshot with { ObjectVersions = objectVersions };
    }

    private static ComponentSnapshot MapComponent(ComponentAggregate component) => new(
        component.Id.Value,
        component.Name,
        component.Code,
        component.Mode.ToString(),
        component.TargetEnvironmentIds,
        component.SingleTarget is { } target
            ? new ComponentSingleTargetSnapshot(
                target.Id,
                target.Code,
                target.SubscriptionId,
                target.Region,
                target.DeploymentConnection,
                target.IsProtected,
                target.Approvers,
                target.Runners)
            : null,
        component.ResourceGroups.Select(group => new ResourceGroupSnapshot(
            group.Id,
            group.Name,
            group.Region,
            group.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase),
            group.ForcedNames.ToDictionary(value => value.EnvironmentId, value => value.Name)) { Version = group.Version }).ToArray(),
        component.Resources.Select(resource => new ResourceSnapshot(
            resource.Id,
            resource.ResourceGroupId,
            resource.Type,
            resource.LogicalName,
            resource.Description,
            resource.IsExisting,
            resource.GetProperties(),
            resource.GetOverrides(),
            resource.Presences.ToDictionary(value => value.TargetId, value => value.IsPresent),
            resource.ExistingIds.ToDictionary(value => value.TargetId, value => value.Value),
            resource.ForcedNames.ToDictionary(value => value.TargetId, value => value.Name),
            resource.Tags.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase),
            resource.Children.Select(child => new ResourceChildSnapshot(
                child.Id,
                child.Type,
                child.Name,
                child.GetProperties())).ToArray())
        {
            DefaultIdentityResourceId = resource.DefaultIdentityResourceId,
            Version = resource.Version
        }).ToArray())
    {
        Version = component.Version,
        TargetsAllEnvironments = component.TargetsAllEnvironments,
        Naming = component.DefaultNameTemplate is not null || component.NamingOverrides.Count > 0
            ? new ComponentNamingSnapshot(
                component.DefaultNameTemplate,
                component.NamingOverrides
                    .Where(value => value.Template is not null)
                    .ToDictionary(value => value.Type, value => value.Template!, StringComparer.OrdinalIgnoreCase),
                component.NamingOverrides
                    .Where(value => value.Abbreviation is not null)
                    .ToDictionary(value => value.Type, value => value.Abbreviation!, StringComparer.OrdinalIgnoreCase))
            : null,
        DefaultLogAnalyticsResourceId = component.DefaultLogAnalyticsResourceId,
        Tags = component.Tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase)
    };
}
