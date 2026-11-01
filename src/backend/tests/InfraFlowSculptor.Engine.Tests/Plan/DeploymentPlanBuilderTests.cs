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
using InfraFlowSculptor.Engine.Tests.Builders;
using System.Text.Json;

namespace InfraFlowSculptor.Engine.Tests.Plan;

public sealed class DeploymentPlanBuilderTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.6")!;
    private static readonly Guid DevId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid PrdId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SharedId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SubscriptionId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void DataAccessDoesNotUseAnotherEnvironmentWhenTargetCodeIsMissing()
    {
        var ordersId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        var dataGroupId = Guid.NewGuid();
        var orders = CreateComponent(ordersId, "orders", [PrdId],
            CreateResource(applicationId, dataGroupId: Guid.NewGuid(), "ContainerApp"));
        var data = CreateComponent(dataId, "data", [DevId, SharedId],
            CreateResource(serverId, dataGroupId, "SqlServer"),
            CreateResource(databaseId, dataGroupId, "SqlDatabase"));
        var snapshot = CreateSnapshot([DevId, PrdId, SharedId], [orders, data], [
            CreateLink(dataId, databaseId, dataId, serverId, LinkKind.Hosting),
            CreateLink(ordersId, applicationId, dataId, databaseId, LinkKind.DataAccess,
                LinkIdentityKind.System, DataAccessLevel.LectureEcriture)
        ]);

        var plan = BuildPlan(snapshot);

        var ordersTarget = Assert.Single(Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets);
        Assert.Equal("prd", ordersTarget.Target.Code);
        Assert.Empty(ordersTarget.DataAccess);
    }

    [Fact]
    public void DataAccessUsesTheSingleComponentTargetForAnotherEnvironment()
    {
        var ordersId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        var dataGroupId = Guid.NewGuid();
        var orders = CreateComponent(ordersId, "orders", [PrdId],
            CreateResource(applicationId, Guid.NewGuid(), "ContainerApp"));
        var data = CreateSingleComponent(dataId,
            CreateResource(serverId, dataGroupId, "SqlServer"),
            CreateResource(databaseId, dataGroupId, "SqlDatabase"));
        var snapshot = CreateSnapshot([DevId, PrdId], [orders, data], [
            CreateLink(dataId, databaseId, dataId, serverId, LinkKind.Hosting),
            CreateLink(ordersId, applicationId, dataId, databaseId, LinkKind.DataAccess,
                LinkIdentityKind.System, DataAccessLevel.LectureEcriture)
        ]);

        var plan = BuildPlan(snapshot);

        var ordersTarget = Assert.Single(Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets);
        Assert.Equal("prd", ordersTarget.Target.Code);
        Assert.Single(ordersTarget.DataAccess);
    }

    [Fact]
    public void DataAccessUsesLogicalDatabaseNameWhenNamingEntryIsMissing()
    {
        var ordersId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var databaseId = Guid.NewGuid();
        var dataGroupId = Guid.NewGuid();
        var database = CreateResource(databaseId, dataGroupId, "SqlDatabase");
        var orders = CreateComponent(ordersId, "orders", [DevId],
            CreateResource(applicationId, Guid.NewGuid(), "ContainerApp"));
        var data = CreateComponent(dataId, "data", [DevId, SharedId],
            CreateResource(serverId, dataGroupId, "SqlServer"), database);
        var snapshot = CreateSnapshot([DevId, SharedId], [orders, data], [
            CreateLink(dataId, databaseId, dataId, serverId, LinkKind.Hosting),
            CreateLink(ordersId, applicationId, dataId, databaseId, LinkKind.DataAccess,
                LinkIdentityKind.System, DataAccessLevel.LectureEcriture)
        ]);
        var linkKinds = CreateLinkKinds();
        var wiring = CreateWiringEngine(linkKinds).Evaluate(snapshot, Catalog);
        var namingPreview = NamingEngine.Preview(snapshot, linkKinds);
        var naming = namingPreview with
        {
            Items = namingPreview.Items
                .Where(item => item.ResourceId != databaseId || item.TargetId != DevId)
                .ToArray()
        };

        var plan = BuildPlan(snapshot, wiring, linkKinds, naming);

        var target = Assert.Single(Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets);
        Assert.Equal(database.LogicalName, Assert.Single(target.DataAccess).Database);
    }

    [Fact]
    public void SharedIdentityReceivesTheMostPermissiveDataAccessRequestedForDatabase()
    {
        var ordersId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var dataId = Guid.Parse("20000000-0000-4000-8000-000000000001");
        var readApplicationId = Guid.Parse("30000000-0000-4000-8000-000000000001");
        var writeApplicationId = Guid.Parse("30000000-0000-4000-8000-000000000002");
        var identityId = Guid.Parse("30000000-0000-4000-8000-000000000003");
        var serverId = Guid.Parse("40000000-0000-4000-8000-000000000001");
        var databaseId = Guid.Parse("40000000-0000-4000-8000-000000000002");
        var dataGroupId = Guid.NewGuid();
        var orders = CreateComponent(ordersId, "orders", [DevId],
            CreateResource(readApplicationId, Guid.NewGuid(), "ContainerApp"),
            CreateResource(writeApplicationId, Guid.NewGuid(), "ContainerApp"),
            CreateResource(identityId, Guid.NewGuid(), "UserAssignedIdentity"));
        var data = CreateComponent(dataId, "data", [DevId],
            CreateResource(serverId, dataGroupId, "SqlServer"),
            CreateResource(databaseId, dataGroupId, "SqlDatabase"));
        var snapshot = CreateSnapshot([DevId], [orders, data], [
            CreateLink(dataId, databaseId, dataId, serverId, LinkKind.Hosting),
            CreateLink(ordersId, readApplicationId, dataId, databaseId, LinkKind.DataAccess,
                LinkIdentityKind.UserAssigned, DataAccessLevel.Lecture, identityResourceId: identityId),
            CreateLink(ordersId, writeApplicationId, dataId, databaseId, LinkKind.DataAccess,
                LinkIdentityKind.UserAssigned, DataAccessLevel.LectureEcriture, identityResourceId: identityId)
        ]);

        var plan = BuildPlan(snapshot);

        var target = Assert.Single(Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets);
        var access = Assert.Single(target.DataAccess);
        Assert.Equal("ReadWrite", access.Level);
        Assert.Equal(["db_datareader", "db_datawriter"], access.DatabaseRoles);
        Assert.Contains("$(Roles)", access.ApplyScript, StringComparison.Ordinal);
        Assert.Equal("ReadWrite", Assert.Single(target.Release.DataAccess).Level);
    }

    [Fact]
    public void SensitiveImplicitApplicationSettingDoesNotExposeItsValue()
    {
        var componentId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var component = CreateComponent(componentId, "orders", [DevId],
            CreateResource(applicationId, Guid.NewGuid(), "ContainerApp"));
        var snapshot = CreateSnapshot([DevId], [component]);
        var linkKinds = CreateLinkKinds();
        var wiringEngine = CreateWiringEngine(linkKinds);
        var wiring = wiringEngine.Evaluate(snapshot, Catalog) with
        {
            ImplicitApplicationSettings = [new ImplicitApplicationSetting(
                Guid.NewGuid(), applicationId, "SENSITIVE_OUTPUT", null, "secretOutput",
                "must-not-be-returned", IsSensitive: true, "Test")]
        };

        var plan = BuildPlan(snapshot, wiring, linkKinds);

        var appSetting = Assert.Single(Assert.Single(plan.Components).Targets.Single().AppSettings);
        Assert.True(appSetting.IsSensitive);
        Assert.Null(appSetting.Value);
    }

    [Fact]
    public void DiagnosticSettingsResolveLocalAndExternalWorkspacesWithReferenceDefaults()
    {
        var coreId = Guid.Parse("50000000-0000-4000-8000-000000000001");
        var ordersId = Guid.Parse("50000000-0000-4000-8000-000000000002");
        var workspaceId = Guid.Parse("50000000-0000-4000-8000-000000000003");
        var appInsightsId = Guid.Parse("50000000-0000-4000-8000-000000000004");
        var keyVaultId = Guid.Parse("50000000-0000-4000-8000-000000000005");
        var environmentId = Guid.Parse("50000000-0000-4000-8000-000000000006");
        var apiId = Guid.Parse("50000000-0000-4000-8000-000000000007");
        var coreGroupId = Guid.Parse("50000000-0000-4000-8000-000000000008");
        var ordersGroupId = Guid.Parse("50000000-0000-4000-8000-000000000009");
        var workspace = CreateResource(workspaceId, coreGroupId, "LogAnalyticsWorkspace") with { LogicalName = "main" };
        var appInsights = CreateResource(appInsightsId, coreGroupId, "ApplicationInsights") with { LogicalName = "main" };
        var keyVault = CreateResource(keyVaultId, coreGroupId, "KeyVault",
            new Dictionary<Guid, bool> { [PrdId] = false }) with { LogicalName = "main" };
        var core = CreateComponent(coreId, "core", [DevId, PrdId], workspace, appInsights, keyVault) with
        {
            DefaultLogAnalyticsResourceId = workspaceId
        };
        var environment = CreateResource(environmentId, ordersGroupId, "ContainerAppsEnvironment") with { LogicalName = "main" };
        var api = CreateResource(apiId, ordersGroupId, "ContainerApp") with { LogicalName = "api" };
        var orders = CreateComponent(ordersId, "orders", [DevId, PrdId], environment, api) with
        {
            DefaultLogAnalyticsResourceId = workspaceId
        };
        var snapshot = CreateSnapshot([DevId, PrdId], [core, orders], [
            CreateLink(ordersId, apiId, ordersId, environmentId, LinkKind.Hosting)
        ]);

        var first = BuildPlan(snapshot);
        var second = BuildPlan(snapshot);

        var coreDev = Assert.Single(
            Assert.Single(first.Components, component => component.ComponentCode == "core").Targets,
            target => target.Target.Code == "dev");
        var coreSettings = coreDev.DiagnosticSettings.OrderBy(setting => setting.SourceResourceType, StringComparer.Ordinal).ToArray();
        Assert.Contains("Microsoft.Insights/diagnosticSettings", coreDev.Release.AccessObjectTypes);
        Assert.Collection(coreSettings,
            setting =>
            {
                Assert.Equal("ApplicationInsights", setting.SourceResourceType);
                Assert.Equal("main", setting.SourceLogicalName);
                Assert.Equal("diag-appi-shop-main-dev", setting.Name);
                Assert.Equal("allLogs", setting.LogCategoryGroup);
                Assert.Equal("AllMetrics", setting.MetricCategory);
                Assert.Equal("Dedicated", setting.LogAnalyticsDestinationType);
                Assert.Empty(setting.ExcludedCategories);
                Assert.Equal("core", setting.Destination.ComponentCode);
                Assert.Equal("dev", setting.Destination.TargetCode);
                Assert.Equal("main", setting.Destination.LogicalName);
                Assert.False(setting.Destination.IsExternal);
            },
            setting =>
            {
                Assert.Equal("KeyVault", setting.SourceResourceType);
                Assert.Equal("diag-kv-shop-main-dev", setting.Name);
            });

        var ordersDev = Assert.Single(
            Assert.Single(first.Components, component => component.ComponentCode == "orders").Targets,
            target => target.Target.Code == "dev");
        Assert.Contains("Microsoft.Insights/diagnosticSettings", ordersDev.Release.AccessObjectTypes);
        Assert.Collection(ordersDev.DiagnosticSettings.OrderBy(setting => setting.SourceResourceType, StringComparer.Ordinal),
            setting =>
            {
                Assert.Equal("ContainerApp", setting.SourceResourceType);
                Assert.Equal("diag-ca-shop-api-dev", setting.Name);
                Assert.Null(setting.LogCategoryGroup);
                Assert.Equal("AllMetrics", setting.MetricCategory);
                Assert.True(setting.Destination.IsExternal);
                Assert.Equal("core", setting.Destination.ComponentCode);
                Assert.Equal("main", setting.Destination.LogicalName);
            },
            setting =>
            {
                Assert.Equal("ContainerAppsEnvironment", setting.SourceResourceType);
                Assert.Equal("diag-cae-shop-main-dev", setting.Name);
                Assert.Equal("allLogs", setting.LogCategoryGroup);
                Assert.Equal("AllMetrics", setting.MetricCategory);
                Assert.True(setting.Destination.IsExternal);
            });

        var corePrd = Assert.Single(
            Assert.Single(first.Components, component => component.ComponentCode == "core").Targets,
            target => target.Target.Code == "prd");
        Assert.DoesNotContain(corePrd.DiagnosticSettings, setting => setting.SourceResourceType == "KeyVault");

        Assert.Equal(
            DeploymentPlanJsonSerializer.Serialize(first),
            DeploymentPlanJsonSerializer.Serialize(second));
    }

    [Fact]
    public void ResourcePlansExposeResolvedLocalHostingDependencies()
    {
        var ordersId = Guid.Parse("51000000-0000-4000-8000-000000000001");
        var environmentId = Guid.Parse("51000000-0000-4000-8000-000000000002");
        var applicationId = Guid.Parse("51000000-0000-4000-8000-000000000003");
        var groupId = Guid.Parse("51000000-0000-4000-8000-000000000004");
        var environment = CreateResource(environmentId, groupId, "ContainerAppsEnvironment") with { LogicalName = "main" };
        var application = CreateResource(applicationId, groupId, "ContainerApp") with { LogicalName = "api" };
        var component = CreateComponent(ordersId, "orders", [DevId], environment, application);
        var snapshot = CreateSnapshot([DevId], [component], [
            CreateLink(ordersId, applicationId, ordersId, environmentId, LinkKind.Hosting)
        ]);

        var plan = BuildPlan(snapshot);

        using var document = JsonDocument.Parse(DeploymentPlanJsonSerializer.Serialize(plan));
        var resources = document.RootElement.GetProperty("components")[0]
            .GetProperty("targets")[0]
            .GetProperty("resources");
        var api = resources.EnumerateArray()
            .Single(resource => resource.GetProperty("logicalName").GetString() == "api");

        Assert.True(api.TryGetProperty("dependencies", out var dependencies));
        var dependency = Assert.Single(dependencies.EnumerateArray());
        Assert.Equal("Hosting", dependency.GetProperty("kind").GetString());
        Assert.Equal("orders", dependency.GetProperty("componentCode").GetString());
        Assert.Equal("dev", dependency.GetProperty("targetCode").GetString());
        Assert.Equal("ContainerAppsEnvironment", dependency.GetProperty("resourceType").GetString());
        Assert.Equal("main", dependency.GetProperty("logicalName").GetString());
        Assert.Equal("cae-shop-main-dev", dependency.GetProperty("name").GetString());
        Assert.False(dependency.GetProperty("isExternal").GetBoolean());
        Assert.True(dependency.GetProperty("createsDeploymentDependency").GetBoolean());
    }

    [Fact]
    public void ResourcePlansDoNotCreateDeploymentDependenciesForAbsentTargetResources()
    {
        var ordersId = Guid.Parse("51500000-0000-4000-8000-000000000001");
        var environmentId = Guid.Parse("51500000-0000-4000-8000-000000000002");
        var applicationId = Guid.Parse("51500000-0000-4000-8000-000000000003");
        var groupId = Guid.Parse("51500000-0000-4000-8000-000000000004");
        var environment = CreateResource(
            environmentId,
            groupId,
            "ContainerAppsEnvironment",
            new Dictionary<Guid, bool> { [DevId] = false }) with { LogicalName = "main" };
        var application = CreateResource(applicationId, groupId, "ContainerApp") with { LogicalName = "api" };
        var component = CreateComponent(ordersId, "orders", [DevId], environment, application);
        var snapshot = CreateSnapshot([DevId], [component], [
            CreateLink(ordersId, applicationId, ordersId, environmentId, LinkKind.Hosting)
        ]);

        var plan = BuildPlan(snapshot);
        var target = Assert.Single(Assert.Single(plan.Components, item => item.ComponentCode == "orders").Targets);
        var dependency = Assert.Single(Assert.Single(target.Resources, resource => resource.LogicalName == "api").Dependencies);

        Assert.Equal("main", dependency.LogicalName);
        Assert.False(dependency.CreatesDeploymentDependency);
    }

    [Fact]
    public void ResourcePlansExposeResolvedExternalDependencies()
    {
        var ordersId = Guid.Parse("52000000-0000-4000-8000-000000000001");
        var coreId = Guid.Parse("52000000-0000-4000-8000-000000000002");
        var workspaceId = Guid.Parse("52000000-0000-4000-8000-000000000003");
        var applicationId = Guid.Parse("52000000-0000-4000-8000-000000000004");
        var workspace = CreateResource(workspaceId, Guid.NewGuid(), "ApplicationInsights") with { LogicalName = "main" };
        var core = CreateComponent(coreId, "core", [DevId], workspace);
        var application = CreateResource(applicationId, Guid.NewGuid(), "ContainerApp") with { LogicalName = "api" };
        var orders = CreateComponent(ordersId, "orders", [DevId], application);
        var snapshot = CreateSnapshot([DevId], [core, orders], [
            CreateLink(ordersId, applicationId, coreId, workspaceId, LinkKind.Telemetry)
        ]);

        var plan = BuildPlan(snapshot);

        var target = Assert.Single(Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets);
        var app = Assert.Single(target.Resources, resource => resource.LogicalName == "api");
        var dependency = Assert.Single(app.Dependencies);
        Assert.Equal("core", dependency.ComponentCode);
        Assert.Equal("ApplicationInsights", dependency.ResourceType);
        Assert.True(dependency.IsExternal);
        Assert.True(dependency.CreatesDeploymentDependency);
        Assert.Equal(SubscriptionId, dependency.SubscriptionId);
        Assert.NotNull(dependency.ResourceId);
    }

    [Fact]
    public void AbsentApplicationOmitsItsSettingsAndRoleAssignmentsForTarget()
    {
        var ordersId = Guid.NewGuid();
        var coreId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var orders = CreateComponent(ordersId, "orders", [DevId, PrdId],
            CreateResource(applicationId, Guid.NewGuid(), "ContainerApp", new Dictionary<Guid, bool> { [PrdId] = false }),
            CreateResource(identityId, Guid.NewGuid(), "UserAssignedIdentity", new Dictionary<Guid, bool> { [PrdId] = false }));
        var core = CreateComponent(coreId, "core", [DevId, PrdId],
            CreateResource(workspaceId, Guid.NewGuid(), "LogAnalyticsWorkspace"));
        var snapshot = CreateSnapshot([DevId, PrdId], [orders, core], [
            CreateLink(ordersId, applicationId, coreId, workspaceId, LinkKind.Access,
                LinkIdentityKind.UserAssigned, roles: ["Log Analytics Reader"], identityResourceId: identityId)
        ]) with
        {
            AppSettings = [new AppSettingSnapshot(
                Guid.NewGuid(), ordersId, applicationId, "PUBLIC_ENDPOINT",
                AppSettingSourceKind.Literal, "https://example.test", new Dictionary<Guid, string>(),
                null, null, null, null, null, null, null)]
        };
        var linkKinds = CreateLinkKinds();
        var wiring = CreateWiringEngine(linkKinds).Evaluate(snapshot, Catalog);
        Assert.Single(wiring.RoleAssignments);

        var plan = BuildPlan(snapshot, wiring, linkKinds);

        var target = Assert.Single(
            Assert.Single(plan.Components, component => component.ComponentCode == "orders").Targets,
            candidate => candidate.Target.Code == "prd");
        Assert.Empty(target.AppSettings);
        Assert.Empty(target.RoleAssignments);
        Assert.DoesNotContain("Microsoft.Authorization/roleAssignments", target.Release.AccessObjectTypes);
    }

    private static DeploymentPlan BuildPlan(
        ModelSnapshot snapshot,
        WiringResult? wiring = null,
        LinkKindRegistry? linkKinds = null,
        NamingPreviewResult? naming = null)
    {
        linkKinds ??= CreateLinkKinds();
        wiring ??= CreateWiringEngine(linkKinds).Evaluate(snapshot, Catalog);
        var ordering = ComponentOrdering.Order(snapshot, Catalog, wiring);
        naming ??= NamingEngine.Preview(snapshot, linkKinds);
        var builder = new DeploymentPlanBuilder(
            linkKinds,
            new ExposureResolver(new ExposureHandlerRegistry(PilotExposureHandlers.Create())),
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
        return builder.Build(snapshot, Catalog, ordering, naming);
    }

    private static LinkKindRegistry CreateLinkKinds() => new(PilotLinkKindHandlers.Create());

    private static WiringEngine CreateWiringEngine(LinkKindRegistry linkKinds) => new(
        linkKinds,
        new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]));

    private static ModelSnapshot CreateSnapshot(
        IReadOnlyList<Guid> environmentIds,
        IReadOnlyList<ComponentSnapshot> components,
        IReadOnlyList<LinkSnapshot>? links = null)
    {
        var builder = new ModelSnapshotBuilder()
            .WithProject(new ProjectSnapshot("Shop", "shop", string.Empty) { CatalogVersion = Catalog.Version });
        foreach (var environmentId in environmentIds)
        {
            builder.WithEnvironment(CreateEnvironment(environmentId));
        }

        foreach (var component in components)
        {
            builder.WithComponent(component);
        }

        return builder.Build() with { Links = links ?? [] };
    }

    private static EnvironmentSnapshot CreateEnvironment(Guid id) => id switch
    {
        var value when value == DevId => CreateEnvironment(id, "dev", 1),
        var value when value == PrdId => CreateEnvironment(id, "prd", 2),
        var value when value == SharedId => CreateEnvironment(id, "shared", 3),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static EnvironmentSnapshot CreateEnvironment(Guid id, string code, int position) =>
        new(code, "francecentral")
        {
            Id = id,
            Name = code,
            Position = position,
            SubscriptionId = SubscriptionId,
            DeploymentConnection = $"{code}-connection"
        };

    private static ComponentSnapshot CreateComponent(
        Guid id,
        string code,
        IReadOnlyList<Guid> targetIds,
        params ResourceSnapshot[] resources)
    {
        var groups = resources.Select(resource => resource.ResourceGroupId).Distinct()
            .Select(groupId => new ResourceGroupSnapshot(groupId, $"{code}-group", null,
                new Dictionary<string, string>(), new Dictionary<Guid, string>()))
            .ToArray();
        return new ComponentSnapshot(id, code, code, "PerEnvironment", targetIds, null, groups, resources);
    }

    private static ComponentSnapshot CreateSingleComponent(Guid id, params ResourceSnapshot[] resources)
    {
        var component = CreateComponent(id, "data", [], resources);
        return component with
        {
            Mode = "Single",
            SingleTarget = new ComponentSingleTargetSnapshot(
                SharedId, "shared", SubscriptionId, "francecentral", "shared-connection", false, [], [])
        };
    }

    private static ResourceSnapshot CreateResource(
        Guid id,
        Guid dataGroupId,
        string type,
        IReadOnlyDictionary<Guid, bool>? presences = null) => new(
            id,
            dataGroupId,
            type,
            $"{type.ToLowerInvariant()}-{id.ToString("N")[..8]}",
            string.Empty,
            false,
            new Dictionary<string, System.Text.Json.JsonElement>(),
            new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>(),
            presences ?? new Dictionary<Guid, bool>(),
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, string>(),
            new Dictionary<string, string>(),
            []);

    private static LinkSnapshot CreateLink(
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind = LinkIdentityKind.None,
        DataAccessLevel? dataAccessLevel = null,
        IReadOnlyList<string>? roles = null,
        Guid? identityResourceId = null) => new(
            Guid.NewGuid(), sourceComponentId, sourceResourceId, targetComponentId, targetResourceId, kind,
            LinkOrigin.Explicit, identityKind, identityResourceId, roles ?? [], LinkAccessScope.Resource, dataAccessLevel);
}
