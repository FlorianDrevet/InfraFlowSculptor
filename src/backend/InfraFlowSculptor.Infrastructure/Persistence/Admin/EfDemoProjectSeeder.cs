using System.Text.Json;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfDemoProjectSeeder(IfsDbContext dbContext)
{
    private static readonly Guid SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    // Keep the reference model's logical target C distinct so J0-20 shows the external-scope finding.
    private static readonly Guid SharedSubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyProperties =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public async Task SeedShopAsync(
        ProjectId projectId,
        OrganizationId organizationId,
        UserProfileId ownerId,
        UserProfileId contributorId,
        UserProfileId readerId,
        string catalogVersion,
        bool seedReferenceModel = false,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .IgnoreQueryFilters()
            .Include(candidate => candidate.RoleAssignments)
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            var ownerTenantId = await dbContext.UserProfiles
                .Where(profile => profile.Id == ownerId)
                .Select(profile => profile.TenantId)
                .SingleAsync(cancellationToken);
            project = ProjectAggregate.Create(
                projectId,
                organizationId,
                seedReferenceModel ? "Boutique en ligne" : "shop",
                "shop",
                ownerTenantId,
                string.Empty,
                catalogVersion,
                ownerId).Value;
            dbContext.Projects.Add(project);
        }
        else if (project.OrganizationId != organizationId)
        {
            throw new InvalidOperationException("The demo project identifier is already used by another organization.");
        }

        AssignIfMissing(project, contributorId, ProjectRole.Contributor);
        AssignIfMissing(project, readerId, ProjectRole.Reader);

        if (seedReferenceModel)
        {
            await SeedReferenceModelAsync(project, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedReferenceModelAsync(ProjectAggregate project, CancellationToken cancellationToken)
    {
        var existingComponents = await dbContext.Components
            .IgnoreQueryFilters()
            .Where(component => component.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        if (existingComponents.Length > 0)
        {
            return;
        }

        EnsureEnvironment(project, "Development", "dev", 1, isProtected: false, []);
        EnsureEnvironment(project, "Production", "prd", 2, isProtected: true, ["Shop Release Approvers"]);

        var environments = project.Environments.OrderBy(environment => environment.Position).ToArray();
        var environmentIds = environments.Select(environment => environment.Id).ToArray();
        var coreId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var ordersId = Guid.NewGuid();

        var core = ComponentAggregate.Create(
            new ComponentId(coreId), project.Id, project.OrganizationId, "Core", "core", "",
            ComponentDeploymentMode.PerEnvironment, environments).Value;
        var data = ComponentAggregate.Create(
            new ComponentId(dataId), project.Id, project.OrganizationId, "Data", "data", "",
            ComponentDeploymentMode.PerEnvironment, environments).Value;
        var orders = ComponentAggregate.Create(
            new ComponentId(ordersId), project.Id, project.OrganizationId, "Orders", "orders", "",
            ComponentDeploymentMode.PerEnvironment, environments).Value;

        var sharedTarget = ComponentSingleTarget.Create(
            project.Code,
            "shared",
            SharedSubscriptionId,
            "francecentral",
            deploymentConnection: null,
            isProtected: false,
            approvers: [],
            runners: [],
            projectEnvironments: environments,
            projectDefaultRunners: project.DefaultRunners).Value;
        var platform = ComponentAggregate.Create(
            new ComponentId(platformId), project.Id, project.OrganizationId, "Platform", "platform", "",
            ComponentDeploymentMode.Single, environments, targetsAllEnvironments: false,
            targetEnvironmentIds: [], singleTarget: sharedTarget).Value;

        var coreGroup = core.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var dataGroup = data.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var platformGroup = platform.AddResourceGroup(Guid.NewGuid(), "main").Value;
        var ordersGroup = orders.AddResourceGroup(Guid.NewGuid(), "main").Value;

        var workspace = AddResource(core, coreGroup.Id, "LogAnalyticsWorkspace", "main", environmentIds);
        var insights = AddResource(core, coreGroup.Id, "ApplicationInsights", "main", environmentIds);
        _ = AddResource(core, coreGroup.Id, "KeyVault", "main", environmentIds);
        var sqlServer = AddResource(data, dataGroup.Id, "SqlServer", "orders", environmentIds);
        var sqlDatabase = AddResource(data, dataGroup.Id, "SqlDatabase", "orders", environmentIds);
        var registry = AddResource(platform, platformGroup.Id, "ContainerRegistry", "main", [sharedTarget.Id]);
        var identity = AddResource(orders, ordersGroup.Id, "UserAssignedIdentity", "api", environmentIds);
        var containerEnvironment = AddResource(orders, ordersGroup.Id, "ContainerAppsEnvironment", "main", environmentIds);
        var applicationResource = orders.AddResource(
            Guid.NewGuid(), ordersGroup.Id, "ContainerApp", "api", "", false, environmentIds,
            EmptyProperties, defaultIdentityResourceId: identity.Id).Value;

        _ = data.SetDefaultLogAnalyticsResource(workspace.Id).Value;
        _ = orders.SetDefaultLogAnalyticsResource(workspace.Id).Value;

        dbContext.Components.AddRange(core, data, platform, orders);
        dbContext.Set<LinkAggregate>().AddRange(
            CreateLink(project, coreId, insights.Id, coreId, workspace.Id, LinkKind.Logging),
            CreateLink(project, dataId, sqlDatabase.Id, dataId, sqlServer.Id, LinkKind.Hosting),
            CreateLink(project, ordersId, containerEnvironment.Id, coreId, workspace.Id, LinkKind.Logging),
            CreateLink(project, ordersId, applicationResource.Id, ordersId, containerEnvironment.Id, LinkKind.Hosting),
            CreateLink(project, ordersId, applicationResource.Id, platformId, registry.Id, LinkKind.ImagePull,
                LinkIdentityKind.UserAssigned, identity.Id),
            CreateLink(project, ordersId, applicationResource.Id, coreId, insights.Id, LinkKind.Telemetry),
            CreateLink(project, ordersId, applicationResource.Id, coreId, workspace.Id, LinkKind.Access,
                LinkIdentityKind.UserAssigned, identity.Id, ["Log Analytics Reader"]),
            CreateLink(project, ordersId, applicationResource.Id, dataId, sqlDatabase.Id, LinkKind.DataAccess,
                LinkIdentityKind.System, dataAccessLevel: DataAccessLevel.LectureEcriture));

        var application = ApplicationAggregate.Create(
            Guid.NewGuid(), project.Id, project.OrganizationId, orders.Id, applicationResource.Id,
            new ApplicationDefinition(
                "api", "src/api", "src/api/Dockerfile", "src/api", "shop/orders/api", null,
                DeploymentStrategyKind.Direct, null, []));
        if (application.IsError)
        {
            throw new InvalidOperationException($"Could not seed the demo application: {application.FirstError.Code}.");
        }

        dbContext.Applications.Add(application.Value);
    }

    private static void EnsureEnvironment(
        ProjectAggregate project,
        string name,
        string code,
        int position,
        bool isProtected,
        IEnumerable<string> approvers)
    {
        if (project.Environments.Any(environment => string.Equals(environment.Code, code, StringComparison.Ordinal)))
        {
            return;
        }

        var created = project.AddEnvironment(
            Guid.NewGuid(), name, code, SubscriptionId, "francecentral", position,
            isProtected, approvers, [], name);
        if (created.IsError)
        {
            throw new InvalidOperationException($"Could not seed demo environment '{code}': {created.FirstError.Code}.");
        }
    }

    private static Resource AddResource(
        ComponentAggregate component,
        Guid resourceGroupId,
        string type,
        string name,
        IEnumerable<Guid> targetIds) =>
        component.AddResource(
            Guid.NewGuid(), resourceGroupId, type, name, "", false, targetIds, EmptyProperties).Value;

    private static LinkAggregate CreateLink(
        ProjectAggregate project,
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind = LinkIdentityKind.None,
        Guid? identityResourceId = null,
        IEnumerable<string>? roles = null,
        DataAccessLevel? dataAccessLevel = null) =>
        LinkAggregate.Create(
            Guid.NewGuid(), project.Id, project.OrganizationId, sourceComponentId, sourceResourceId,
            targetComponentId, targetResourceId, kind, identityKind, identityResourceId, roles,
            LinkAccessScope.Resource, dataAccessLevel).Value;

    private static void AssignIfMissing(ProjectAggregate project, UserProfileId memberId, ProjectRole role)
    {
        if (!project.HasRole(memberId, role))
        {
            var result = project.AssignRole(memberId, role);
            if (result.IsError)
            {
                throw new InvalidOperationException($"Could not seed the demo project role: {result.FirstError.Code}.");
            }
        }
    }
}
