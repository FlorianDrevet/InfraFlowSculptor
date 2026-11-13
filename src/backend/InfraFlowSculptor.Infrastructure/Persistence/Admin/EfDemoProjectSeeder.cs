using System.Text.Json;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfDemoProjectSeeder(IfsDbContext dbContext)
{
    private static readonly Guid DevelopmentSubscriptionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ProductionSubscriptionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    // Keep the reference model's logical target C distinct so J0-20 shows the external-scope finding.
    private static readonly Guid SharedSubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SqlAdminGroupDevelopmentId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid SqlAdminGroupProductionId = Guid.Parse("00000000-0000-4000-8000-000000000002");
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
            _ = project.ConfigureNaming(
                "Compact",
                "{abbr}-{project}-{name}-{env}",
                "rg-{project}-{component}-{name}-{env}").Value;
            _ = project.UpdateSettings(
                project.Name,
                project.Code,
                project.Description,
                systemTagsEnabled: true,
                project.CoordinatorUserProfileId,
                [new KeyValuePair<string, string>("costCenter", "ecommerce")],
                confirmImpact: true).Value;
            await SeedReferenceModelAsync(project, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedReferenceModelAsync(ProjectAggregate project, CancellationToken cancellationToken)
    {
        var existingComponents = await dbContext.Components
            .IgnoreQueryFilters()
            .Include(component => component.Resources)
            .Where(component => component.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        if (existingComponents.Length > 0)
        {
            var existingCore = existingComponents.SingleOrDefault(component => component.Code == "core");
            var existingWorkspace = existingCore?.Resources.SingleOrDefault(
                resource => resource.Type == "LogAnalyticsWorkspace" && resource.LogicalName == "main");
            if (existingCore is { DefaultLogAnalyticsResourceId: null } && existingWorkspace is not null)
            {
                _ = existingCore.SetDefaultLogAnalyticsResource(existingWorkspace.Id).Value;
            }

            return;
        }

        EnsureEnvironment(
            project,
            "Development",
            "dev",
            DevelopmentSubscriptionId,
            1,
            isProtected: false,
            []);
        EnsureEnvironment(
            project,
            "Production",
            "prd",
            ProductionSubscriptionId,
            2,
            isProtected: true,
            ["Shop Release Approvers"]);

        var environments = project.Environments.OrderBy(environment => environment.Position).ToArray();
        var environmentIds = environments.Select(environment => environment.Id).ToArray();
        var developmentEnvironment = environments.Single(environment => environment.Code == "dev");
        var productionEnvironment = environments.Single(environment => environment.Code == "prd");
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
            deploymentConnection: "shared-connection",
            isProtected: true,
            approvers: ["Shop Release Approvers"],
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
        _ = core.SetResourceOverride(
            workspace.Id,
            productionEnvironment.Id,
            "retentionInDays",
            JsonSerializer.SerializeToElement(90)).Value;
        var insights = AddResource(core, coreGroup.Id, "ApplicationInsights", "main", environmentIds);
        var vault = AddResource(core, coreGroup.Id, "KeyVault", "main", environmentIds);
        _ = core.SetResourceOverride(
            vault.Id,
            developmentEnvironment.Id,
            "purgeProtection",
            JsonSerializer.SerializeToElement(false)).Value;
        var sqlServer = AddResource(
            data,
            dataGroup.Id,
            "SqlServer",
            "orders",
            environmentIds,
            Properties(
                ("administratorDisplayName", "sg-shop-sql-admins"),
                ("administratorObjectId", SqlAdminGroupDevelopmentId.ToString("D"))));
        _ = data.SetResourceOverride(
            sqlServer.Id,
            productionEnvironment.Id,
            "administratorObjectId",
            JsonSerializer.SerializeToElement(SqlAdminGroupProductionId.ToString("D"))).Value;
        var sqlDatabase = AddResource(
            data,
            dataGroup.Id,
            "SqlDatabase",
            "orders",
            environmentIds,
            Properties(("sku", "GP_S_Gen5_1")));
        _ = data.SetResourceOverride(
            sqlDatabase.Id,
            productionEnvironment.Id,
            "sku",
            JsonSerializer.SerializeToElement("GP_Gen5_2")).Value;
        var registry = AddResource(platform, platformGroup.Id, "ContainerRegistry", "main", [sharedTarget.Id]);
        var identity = AddResource(orders, ordersGroup.Id, "UserAssignedIdentity", "api", environmentIds);
        var containerEnvironment = AddResource(
            orders,
            ordersGroup.Id,
            "ContainerAppsEnvironment",
            "main",
            environmentIds,
            Properties(("zoneRedundant", false)));
        var applicationResource = orders.AddResource(
            Guid.NewGuid(), ordersGroup.Id, "ContainerApp", "api", "", false, environmentIds,
            EmptyProperties, defaultIdentityResourceId: identity.Id).Value;
        _ = orders.SetResourceOverride(
            applicationResource.Id,
            developmentEnvironment.Id,
            "maxReplicas",
            JsonSerializer.SerializeToElement(2)).Value;
        foreach (var (propertyName, value) in new (string PropertyName, object Value)[]
        {
            ("cpu", 1),
            ("memoryGi", "2Gi"),
            ("minReplicas", 1),
            ("maxReplicas", 3)
        })
        {
            _ = orders.SetResourceOverride(
                applicationResource.Id,
                productionEnvironment.Id,
                propertyName,
                JsonSerializer.SerializeToElement(value)).Value;
        }

        _ = data.SetDefaultLogAnalyticsResource(workspace.Id).Value;
        _ = orders.SetDefaultLogAnalyticsResource(workspace.Id).Value;
        _ = core.SetDefaultLogAnalyticsResource(workspace.Id).Value;

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
                DeploymentStrategyKind.Direct,
                null,
                [
                    new ApplicationStepDefinition(
                        "DependencyCache",
                        true,
                        false,
                        ApplicationStepPosition.BeforeBuild,
                        []),
                    new ApplicationStepDefinition(
                        "ImageScan",
                        true,
                        true,
                        ApplicationStepPosition.AfterBuild,
                        [])
                ]));
        if (application.IsError)
        {
            throw new InvalidOperationException($"Could not seed the demo application: {application.FirstError.Code}.");
        }

        dbContext.Applications.Add(application.Value);

        var paymentKey = AppSettingAggregate.Create(
            Guid.NewGuid(),
            project.Id,
            project.OrganizationId,
            orders.Id.Value,
            applicationResource.Id,
            new AppSettingDefinition(
                "Payments__ApiKey",
                AppSettingSourceKind.KeyVaultSecret,
                KeyVaultResourceId: vault.Id,
                SecretName: "payments-api-key",
                SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
                PipelineVariableName: "MAIN_PAYMENTS_API_KEY",
                IdentityResourceId: identity.Id));
        if (paymentKey.IsError)
        {
            throw new InvalidOperationException($"Could not seed the demo application setting: {paymentKey.FirstError.Code}.");
        }

        dbContext.AppSettings.Add(paymentKey.Value);
    }

    private static void EnsureEnvironment(
        ProjectAggregate project,
        string name,
        string code,
        Guid subscriptionId,
        int position,
        bool isProtected,
        IEnumerable<string> approvers)
    {
        if (project.Environments.Any(environment => string.Equals(environment.Code, code, StringComparison.Ordinal)))
        {
            return;
        }

        var created = project.AddEnvironment(
            Guid.NewGuid(), name, code, subscriptionId, "francecentral", position,
            isProtected, approvers, [], name, deploymentConnection: $"{code}-connection");
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
        IEnumerable<Guid> targetIds,
        IReadOnlyDictionary<string, JsonElement>? properties = null) =>
        component.AddResource(
            Guid.NewGuid(), resourceGroupId, type, name, "", false, targetIds, properties ?? EmptyProperties).Value;

    private static Dictionary<string, JsonElement> Properties(params (string Name, object? Value)[] properties) =>
        properties.ToDictionary(
            property => property.Name,
            property => JsonSerializer.SerializeToElement(property.Value),
            StringComparer.Ordinal);

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
