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

namespace InfraFlowSculptor.Engine.Tests.Plan;

public sealed class DeploymentPlanBuilderTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.5")!;
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
