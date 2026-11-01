using System.Text.Json;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Exposure;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Tests;

internal static class PilotReferenceModel
{
    private static readonly Guid DevelopmentId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid ProductionId = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid SharedId = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static readonly Guid DevelopmentSubscriptionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ProductionSubscriptionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid SharedSubscriptionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid SqlAdminGroupDevelopmentId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid SqlAdminGroupProductionId = Guid.Parse("00000000-0000-4000-8000-000000000002");

    public static DeploymentPlan Create(CatalogVersion catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var snapshot = CreateSnapshot(catalog);
        var linkKinds = new LinkKindRegistry(PilotLinkKindHandlers.Create());
        var wiring = new WiringEngine(
            linkKinds,
            new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]))
            .Evaluate(snapshot, catalog);
        var ordering = ComponentOrdering.Order(snapshot, catalog, wiring);
        var naming = NamingEngine.Preview(snapshot, linkKinds);
        var builder = new DeploymentPlanBuilder(
            linkKinds,
            new ExposureResolver(new ExposureHandlerRegistry(PilotExposureHandlers.Create())),
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));

        return builder.Build(snapshot, catalog, ordering, naming);
    }

    private static ModelSnapshot CreateSnapshot(CatalogVersion catalog)
    {
        var coreId = Guid.Parse("20000000-0000-4000-8000-000000000001");
        var dataId = Guid.Parse("20000000-0000-4000-8000-000000000002");
        var platformId = Guid.Parse("20000000-0000-4000-8000-000000000003");
        var ordersId = Guid.Parse("20000000-0000-4000-8000-000000000004");
        var coreGroupId = Guid.Parse("30000000-0000-4000-8000-000000000001");
        var dataGroupId = Guid.Parse("30000000-0000-4000-8000-000000000002");
        var platformGroupId = Guid.Parse("30000000-0000-4000-8000-000000000003");
        var ordersGroupId = Guid.Parse("30000000-0000-4000-8000-000000000004");
        var workspaceId = Guid.Parse("40000000-0000-4000-8000-000000000001");
        var insightsId = Guid.Parse("40000000-0000-4000-8000-000000000002");
        var vaultId = Guid.Parse("40000000-0000-4000-8000-000000000003");
        var sqlServerId = Guid.Parse("40000000-0000-4000-8000-000000000004");
        var sqlDatabaseId = Guid.Parse("40000000-0000-4000-8000-000000000005");
        var registryId = Guid.Parse("40000000-0000-4000-8000-000000000006");
        var identityId = Guid.Parse("40000000-0000-4000-8000-000000000007");
        var environmentId = Guid.Parse("40000000-0000-4000-8000-000000000008");
        var containerAppId = Guid.Parse("40000000-0000-4000-8000-000000000009");
        var applicationId = Guid.Parse("50000000-0000-4000-8000-000000000001");
        var project = new ProjectSnapshot("Boutique en ligne", "shop", string.Empty)
        {
            CatalogVersion = catalog.Version,
            SystemTagsEnabled = true
        };
        var environments = new[]
        {
            CreateEnvironment("dev", DevelopmentId, DevelopmentSubscriptionId, 1, false),
            CreateEnvironment("prd", ProductionId, ProductionSubscriptionId, 2, true)
        };

        var workspace = CreateResource(
            workspaceId, coreGroupId, "LogAnalyticsWorkspace", "main",
            overrides: Overrides((ProductionId, ("retentionInDays", 90))));
        var insights = CreateResource(insightsId, coreGroupId, "ApplicationInsights", "main");
        var vault = CreateResource(
            vaultId, coreGroupId, "KeyVault", "main",
            overrides: Overrides((DevelopmentId, ("purgeProtection", false))));
        var core = CreateComponent(
            coreId, "Core", "core", [DevelopmentId, ProductionId], coreGroupId,
            [workspace, insights, vault], workspaceId);

        var sqlServer = CreateResource(
            sqlServerId, dataGroupId, "SqlServer", "orders",
            Properties(
                ("administratorDisplayName", "sg-shop-sql-admins"),
                ("administratorObjectId", SqlAdminGroupDevelopmentId.ToString("D"))),
            Overrides((ProductionId, ("administratorObjectId", SqlAdminGroupProductionId.ToString("D")))));
        var sqlDatabase = CreateResource(
            sqlDatabaseId, dataGroupId, "SqlDatabase", "orders",
            Properties(("sku", "GP_S_Gen5_1")),
            Overrides((ProductionId, ("sku", "GP_Gen5_2"))));
        var data = CreateComponent(
            dataId, "Data", "data", [DevelopmentId, ProductionId], dataGroupId,
            [sqlServer, sqlDatabase], workspaceId);

        var registry = CreateResource(registryId, platformGroupId, "ContainerRegistry", "main");
        var platform = new ComponentSnapshot(
            platformId,
            "Platform",
            "platform",
            ComponentDeploymentMode.Single.ToString(),
            [],
            new ComponentSingleTargetSnapshot(
                SharedId, "shared", SharedSubscriptionId, "francecentral", "shared-connection", true,
                ["Shop Release Approvers"], []),
            [CreateGroup(platformGroupId)],
            [registry]);

        var identity = CreateResource(identityId, ordersGroupId, "UserAssignedIdentity", "api");
        var environment = CreateResource(
            environmentId, ordersGroupId, "ContainerAppsEnvironment", "main",
            Properties(("zoneRedundant", true)));
        var containerApp = CreateResource(
            containerAppId, ordersGroupId, "ContainerApp", "api",
            overrides: Overrides(
                (DevelopmentId, ("maxReplicas", 2)),
                (ProductionId, ("cpu", 1)),
                (ProductionId, ("memoryGi", "2Gi")),
                (ProductionId, ("minReplicas", 1)),
                (ProductionId, ("maxReplicas", 3))))
            with { DefaultIdentityResourceId = identityId };
        var orders = CreateComponent(
            ordersId, "Orders", "orders", [DevelopmentId, ProductionId], ordersGroupId,
            [identity, environment, containerApp], workspaceId);

        var links = new[]
        {
            CreateLink(coreId, insightsId, coreId, workspaceId, LinkKind.Logging),
            CreateLink(dataId, sqlDatabaseId, dataId, sqlServerId, LinkKind.Hosting),
            CreateLink(ordersId, environmentId, coreId, workspaceId, LinkKind.Logging),
            CreateLink(ordersId, containerAppId, ordersId, environmentId, LinkKind.Hosting),
            CreateLink(ordersId, containerAppId, platformId, registryId, LinkKind.ImagePull,
                LinkIdentityKind.UserAssigned, identityResourceId: identityId),
            CreateLink(ordersId, containerAppId, coreId, insightsId, LinkKind.Telemetry),
            CreateLink(ordersId, containerAppId, coreId, workspaceId, LinkKind.Access,
                LinkIdentityKind.UserAssigned, roles: ["Log Analytics Reader"], identityResourceId: identityId),
            CreateLink(ordersId, containerAppId, dataId, sqlDatabaseId, LinkKind.DataAccess,
                LinkIdentityKind.System, dataAccessLevel: DataAccessLevel.LectureEcriture)
        };
        var appSetting = new AppSettingSnapshot(
            Guid.Parse("60000000-0000-4000-8000-000000000001"),
            ordersId,
            containerAppId,
            "Payments__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            null,
            new Dictionary<Guid, string>(),
            null,
            null,
            vaultId,
            "payments-api-key",
            AppSettingSecretFeedKind.PipelineSecret,
            "MAIN_PAYMENTS_API_KEY",
            identityId);
        var application = new ApplicationSnapshot(
            applicationId,
            ordersId,
            containerAppId,
            "api",
            "src/api",
            "src/api/Dockerfile",
            "src/api",
            "shop/orders/api",
            null,
            DeploymentStrategyKind.Direct,
            null,
            []);

        return new ModelSnapshot(
            project,
            environments,
            new NamingConventionSnapshot(
                "Compact",
                "{abbr}-{project}-{name}-{env}",
                "rg-{project}-{component}-{name}-{env}"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["costCenter"] = "ecommerce" },
            [core, data, orders, platform])
        {
            NamingRules = CatalogNamingSnapshotMapper.CreateRules(catalog),
            RegionCodes = CatalogNamingSnapshotMapper.CreateRegionCodes(catalog),
            Links = links,
            AppSettings = [appSetting],
            Applications = [application]
        };
    }

    private static EnvironmentSnapshot CreateEnvironment(
        string code,
        Guid id,
        Guid subscriptionId,
        int position,
        bool isProtected) => new(code, "francecentral")
    {
        Id = id,
        Name = code,
        Position = position,
        SubscriptionId = subscriptionId,
        DeploymentConnection = $"{code}-connection",
        IsProtected = isProtected,
        Approvers = isProtected ? ["Shop Release Approvers"] : [],
        Runners = []
    };

    private static ComponentSnapshot CreateComponent(
        Guid id,
        string name,
        string code,
        IReadOnlyList<Guid> targets,
        Guid groupId,
        IReadOnlyList<ResourceSnapshot> resources,
        Guid? defaultLogAnalyticsResourceId) => new(
        id,
        name,
        code,
        ComponentDeploymentMode.PerEnvironment.ToString(),
        targets,
        null,
        [CreateGroup(groupId)],
        resources)
    {
        DefaultLogAnalyticsResourceId = defaultLogAnalyticsResourceId
    };

    private static ResourceGroupSnapshot CreateGroup(Guid id) => new(
        id,
        "main",
        null,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<Guid, string>());

    private static ResourceSnapshot CreateResource(
        Guid id,
        Guid groupId,
        string type,
        string logicalName,
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>? overrides = null) => new(
        id,
        groupId,
        type,
        logicalName,
        string.Empty,
        false,
        properties ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        overrides ?? new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        []);

    private static Dictionary<string, JsonElement> Properties(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>> Overrides(
        params (Guid TargetId, (string Name, object? Value) Property)[] values) => values
        .GroupBy(value => value.TargetId)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyDictionary<string, JsonElement>)group.ToDictionary(
                value => value.Property.Name,
                value => JsonSerializer.SerializeToElement(value.Property.Value),
                StringComparer.Ordinal));

    private static LinkSnapshot CreateLink(
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind = LinkIdentityKind.None,
        IReadOnlyList<string>? roles = null,
        Guid? identityResourceId = null,
        DataAccessLevel? dataAccessLevel = null) => new(
        Guid.NewGuid(),
        sourceComponentId,
        sourceResourceId,
        targetComponentId,
        targetResourceId,
        kind,
        LinkOrigin.Explicit,
        identityKind,
        identityResourceId,
        roles ?? [],
        LinkAccessScope.Resource,
        dataAccessLevel);
}
