using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Tests.Builders;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Tests.Naming;

public sealed class NamingEngineTests
{
    private static readonly LinkKindRegistry LinkKinds = new(PilotLinkKindHandlers.Create());

    private static readonly string[] PilotResourceGroupNames =
    [
        "rg-shop-core-main-dev", "rg-shop-core-main-prd",
        "rg-shop-data-main-dev", "rg-shop-data-main-prd",
        "rg-shop-platform-main-shared",
        "rg-shop-orders-main-dev", "rg-shop-orders-main-prd"
    ];

    [Fact]
    public void CompactPresetPreviewsResourceAndResourceGroupNames()
    {
        var result = Preview(CreateSnapshot(Compact()));

        Assert.Empty(result.Issues);
        var preview = Assert.Single(result.Items);
        Assert.Equal("kv-shop-main-dev", preview.ResourceName);
        Assert.Equal("rg-shop-main-main-dev", preview.ResourceGroupName);
    }

    [Fact]
    public void CafPresetAddsTheEnvironmentRegionToBothNames()
    {
        var result = Preview(CreateSnapshot(Caf()));

        Assert.Empty(result.Issues);
        var preview = Assert.Single(result.Items);
        Assert.Equal("kv-shop-main-dev-frc", preview.ResourceName);
        Assert.Equal("rg-shop-main-main-dev-frc", preview.ResourceGroupName);
    }

    [Fact]
    public void PreviewReportsUnknownTokensAndSupportedTokenNames()
    {
        var custom = new NamingConventionSnapshot(
            "Custom",
            "{abbr}-{project}-{name}-{zone}",
            "rg-{project}-{component}-{name}-{env}");

        var result = Preview(CreateSnapshot(custom));

        var issue = Assert.Single(result.Issues);
        Assert.Equal("NAMING_TOKEN_UNKNOWN", issue.Code);
        Assert.Contains("region", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewRequiresNameTokenInEachTemplate()
    {
        var custom = new NamingConventionSnapshot("Custom", "{abbr}-{project}-{env}", "rg-{project}-{component}-{env}");

        var result = Preview(CreateSnapshot(custom));

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "NAMING_NAME_TOKEN_REQUIRED"));
    }

    [Fact]
    public void PreviewSanitizesNamesForTypesThatDoNotAllowSeparators()
    {
        var targetId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var registry = Resource(groupId, "main", targetId) with { Type = "ContainerRegistry" };
        var component = new ComponentSnapshot(
            Guid.NewGuid(), "API", "api", "Single", [],
            new ComponentSingleTargetSnapshot(targetId, "shared", Guid.NewGuid(), "francecentral", "local", false, [], []),
            [new ResourceGroupSnapshot(groupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [registry]);
        var naming = new NamingConventionSnapshot(
            "Custom", "{abbr}-{project}-{component}-{name}-{env}", "rg-{project}-{component}-{name}-{env}");
        var snapshot = new ModelSnapshotBuilder().WithNaming(naming).WithComponent(component).Build();

        var result = Preview(snapshot);

        Assert.Empty(result.Issues);
        var item = Assert.Single(result.Items);
        Assert.Equal("crshopapimainshared", item.ResourceName);
        Assert.Contains("Caractères interdits retirés", item.Explanation!.Transformations);
    }

    [Fact]
    public void PreviewReturnsOneItemForEachPresentResourceAndTarget()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var firstResource = Resource(resourceGroupId, "vault", targetId);
        var secondResource = Resource(resourceGroupId, "cache", targetId);
        var component = new ComponentSnapshot(
            Guid.NewGuid(),
            "API",
            "api",
            "PerEnvironment",
            [targetId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "shared", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [firstResource, secondResource]);
        var naming = new NamingConventionSnapshot(
            "Custom", "{abbr}-{project}-{component}-{name}-{env}", "rg-{project}-{component}-{name}-{env}");
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(naming)
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        Assert.Empty(result.Issues);
        Assert.Collection(
            result.Items,
            first =>
            {
                Assert.Equal("kv-shop-api-cache-dev", first.ResourceName);
                Assert.Equal("rg-shop-api-shared-dev", first.ResourceGroupName);
            },
            second =>
            {
                Assert.Equal("kv-shop-api-vault-dev", second.ResourceName);
                Assert.Equal("rg-shop-api-shared-dev", second.ResourceGroupName);
            });
    }

    [Fact]
    public void PreviewOmitsResourcesAbsentFromTarget()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "vault", targetId) with
        {
            Presences = new Dictionary<Guid, bool> { [targetId] = false }
        };
        var component = new ComponentSnapshot(
            Guid.NewGuid(),
            "API",
            "api",
            "PerEnvironment",
            [targetId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "shared", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNamingRule("KeyVault/Secret", new NamingRuleSnapshot(
                "", "^[a-zA-Z0-9]{1,24}$", "mixed", [], true, 1, 24, "alphanumeric", "alphanumeric", "parent", null))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        Assert.Empty(result.Issues);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void PreviewReportsExcessiveLengthWithoutTruncatingTheName()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "verylonglogicalnameresourcename", targetId);
        var component = Component(targetId, resourceGroupId, "main", [resource]);
        var naming = new NamingConventionSnapshot("Custom", "{name}", "rg-{name}");
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(naming)
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        var item = Assert.Single(result.Items);
        Assert.Equal("verylonglogicalnameresourcename", item.ResourceName);
        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-LONGUEUR");
        Assert.Equal(item.ResourceName.Length, issue.ValueLength);
        Assert.Equal(24, issue.Limit);
        Assert.Equal(item.ResourceName, issue.Value);
    }

    [Fact]
    public void PreviewUsesComponentTypeTemplateBeforeProjectDefault()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "main", targetId);
        var component = Component(targetId, resourceGroupId, "api", [resource]) with
        {
            Naming = new ComponentNamingSnapshot(
                "{abbr}-{project}-{component}-{name}-{env}",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["KeyVault"] = "{abbr}-{component}-{name}-{env}"
                },
                new Dictionary<string, string>())
        };
        var projectNaming = new NamingConventionSnapshot(
            "Custom",
            "{abbr}-{project}-{name}-{env}",
            "rg-{project}-{component}-{name}-{env}");
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(projectNaming)
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        Assert.Equal("kv-api-main-dev", Assert.Single(result.Items).ResourceName);
    }

    [Fact]
    public void PreviewReportsOrganizationWideNameCollisions()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "main", targetId);
        var component = Component(targetId, resourceGroupId, "main", [resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build() with
        {
            OrganizationGlobalNames = [new GlobalNameReservationSnapshot("KeyVault", "kv-shop-main-dev", Guid.NewGuid())]
        };

        var result = Preview(snapshot);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-COLLISION");
        Assert.Equal("kv-shop-main-dev", issue.Value);
    }

    [Fact]
    public void ParentUniqueResourceNamesCanRepeatAcrossTargets()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var server = Resource(resourceGroupId, "server", devId) with
        {
            Id = serverId,
            Type = "SqlServer",
            Presences = new Dictionary<Guid, bool> { [devId] = true, [prdId] = true }
        };
        var resource = Resource(resourceGroupId, "orders", devId) with
        {
            Type = "SqlDatabase",
            Presences = new Dictionary<Guid, bool> { [devId] = true, [prdId] = true },
            ForcedNames = new Dictionary<Guid, string> { [devId] = "orders", [prdId] = "orders" }
        };
        var component = new ComponentSnapshot(
            componentId,
            "data",
            "data",
            "PerEnvironment",
            [devId, prdId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [server, resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(new NamingConventionSnapshot("Parent", "{name}-{env}", "rg-{name}-{env}"))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral")
            {
                Id = devId,
                Position = 0,
                SubscriptionId = Guid.NewGuid()
            })
            .WithEnvironment(new EnvironmentSnapshot("prd", "francecentral")
            {
                Id = prdId,
                Position = 1,
                SubscriptionId = Guid.NewGuid()
            })
            .WithComponent(component)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(Guid.NewGuid(), componentId, resource.Id, componentId, serverId,
                    LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null)
            ]
        };

        var result = Preview(snapshot);

        Assert.Empty(result.Issues);
        var databaseItems = result.Items.Where(item => item.ResourceId == resource.Id).ToArray();
        Assert.Equal(2, databaseItems.Length);
        Assert.All(databaseItems, item => Assert.Equal("orders", item.ResourceName));
        Assert.Equal(new[] { devId, prdId }.Order(), databaseItems.Select(item => item.TargetId).Order());
    }

    [Fact]
    public void ParentUniqueResourceNamesAreScopedByTheirHostingResource()
    {
        var targetId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var firstServerId = Guid.NewGuid();
        var secondServerId = Guid.NewGuid();
        var firstDatabaseId = Guid.NewGuid();
        var secondDatabaseId = Guid.NewGuid();
        var serverA = Resource(resourceGroupId, "server-a", targetId) with { Type = "SqlServer" };
        var serverB = Resource(resourceGroupId, "server-b", targetId) with { Type = "SqlServer" };
        var databaseA = Resource(resourceGroupId, "database-a", targetId) with
        {
            Id = firstDatabaseId,
            Type = "SqlDatabase",
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "orders" }
        };
        var databaseB = Resource(resourceGroupId, "database-b", targetId) with
        {
            Id = secondDatabaseId,
            Type = "SqlDatabase",
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "orders" }
        };
        var component = new ComponentSnapshot(
            componentId,
            "data",
            "data",
            "PerEnvironment",
            [targetId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [serverA with { Id = firstServerId }, serverB with { Id = secondServerId }, databaseA, databaseB]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(new NamingConventionSnapshot("Parent", "{name}", "rg-{name}-{env}"))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(Guid.NewGuid(), componentId, firstDatabaseId, componentId, firstServerId,
                    LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null),
                new LinkSnapshot(Guid.NewGuid(), componentId, secondDatabaseId, componentId, secondServerId,
                    LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null)
            ]
        };

        var result = Preview(snapshot);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == "VAL-NOM-COLLISION" && issue.ResourceId is not null);
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(2, result.Items.Count(item => item.ResourceType == "SqlDatabase" && item.ResourceName == "orders"));
    }

    [Fact]
    public void ParentUniqueResourceNamesCollideOnTheSameHostingResourceAndTarget()
    {
        var targetId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var firstDatabaseId = Guid.NewGuid();
        var secondDatabaseId = Guid.NewGuid();
        var server = Resource(resourceGroupId, "server", targetId) with { Id = serverId, Type = "SqlServer" };
        var firstDatabase = Resource(resourceGroupId, "first-database", targetId) with
        {
            Id = firstDatabaseId,
            Type = "SqlDatabase",
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "orders" }
        };
        var secondDatabase = Resource(resourceGroupId, "second-database", targetId) with
        {
            Id = secondDatabaseId,
            Type = "SqlDatabase",
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "orders" }
        };
        var component = new ComponentSnapshot(
            componentId,
            "data",
            "data",
            "PerEnvironment",
            [targetId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [server, firstDatabase, secondDatabase]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(new NamingConventionSnapshot("Parent", "{name}", "rg-{name}-{env}"))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build() with
        {
            Links =
            [
                new LinkSnapshot(Guid.NewGuid(), componentId, firstDatabaseId, componentId, serverId,
                    LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null),
                new LinkSnapshot(Guid.NewGuid(), componentId, secondDatabaseId, componentId, serverId,
                    LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null)
            ]
        };

        var result = Preview(snapshot);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-COLLISION");
        Assert.Contains(issue.ResourceId, new Guid?[] { firstDatabaseId, secondDatabaseId });
        Assert.Equal("orders", issue.Value);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public void PreviewReportsCollisionWithExistingGlobalResource()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var generated = Resource(resourceGroupId, "main", targetId);
        var existing = Resource(resourceGroupId, "legacy", targetId) with
        {
            IsExisting = true,
            ExistingIds = new Dictionary<Guid, string>
            {
                [targetId] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/legacy/providers/Microsoft.KeyVault/vaults/kv-shop-main-dev"
            }
        };
        var component = Component(targetId, resourceGroupId, "main", [existing, generated]);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-COLLISION");
        Assert.Equal(generated.Id, issue.ResourceId);
        Assert.Equal("kv-shop-main-dev", issue.Value);
    }

    [Fact]
    public void PreviewReportsCollisionBetweenExistingAndExternallyReservedGlobalResource()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        const string azureName = "kv-shop-main-dev";
        var existing = Resource(resourceGroupId, "legacy", targetId) with
        {
            IsExisting = true,
            ExistingIds = new Dictionary<Guid, string>
            {
                [targetId] = $"/subscriptions/{Guid.NewGuid():D}/resourceGroups/legacy/providers/Microsoft.KeyVault/vaults/{azureName}"
            }
        };
        var component = Component(targetId, resourceGroupId, "main", [existing]);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build() with
        {
            OrganizationGlobalNames = [new GlobalNameReservationSnapshot("KeyVault", azureName, Guid.NewGuid())]
        };

        var result = Preview(snapshot);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-COLLISION");
        Assert.Equal(existing.Id, issue.ResourceId);
        Assert.Equal(azureName, issue.Value);
    }

    [Fact]
    public void PreviewDoesNotSanitizeInvalidForcedNames()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "main", targetId) with
        {
            ForcedNames = new Dictionary<Guid, string> { [targetId] = "not_valid" }
        };
        var component = Component(targetId, resourceGroupId, "main", [resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        var item = Assert.Single(result.Items);
        Assert.Equal("not_valid", item.ResourceName);
        Assert.Contains(result.Issues, issue => issue.Code == "RESOURCE_FORCED_NAME_INVALID");
    }

    [Fact]
    public void PreviewValidatesChildNamesAgainstTheParentDescriptor()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "main", targetId) with
        {
            Children = [new ResourceChildSnapshot(Guid.NewGuid(), "Secret", "bad-name", new Dictionary<string, System.Text.Json.JsonElement>())]
        };
        var component = Component(targetId, resourceGroupId, "main", [resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNamingRule("KeyVault/Secret", new NamingRuleSnapshot(
                "", "^[a-zA-Z0-9]{1,24}$", "mixed", [], true, 1, 24, "alphanumeric", "alphanumeric", "parent", null))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        Assert.Contains(result.Issues, issue => issue.Code == "RESOURCE_CHILD_NAME_INVALID" && issue.Value == "bad-name");
    }

    [Fact]
    public void PreviewReportsOneTargetIndependentCollisionForDuplicateChildNames()
    {
        var targetId = Guid.NewGuid();
        var resourceGroupId = Guid.NewGuid();
        var resource = Resource(resourceGroupId, "main", targetId) with
        {
            Children =
            [
                new ResourceChildSnapshot(Guid.NewGuid(), "Secret", "token", new Dictionary<string, System.Text.Json.JsonElement>()),
                new ResourceChildSnapshot(Guid.NewGuid(), "Secret", "token", new Dictionary<string, System.Text.Json.JsonElement>())
            ]
        };
        var component = Component(targetId, resourceGroupId, "main", [resource]);
        var snapshot = new ModelSnapshotBuilder()
            .WithNamingRule("KeyVault/Secret", new NamingRuleSnapshot(
                "", "^[a-zA-Z0-9]{1,24}$", "mixed", [], true, 1, 24, "alphanumeric", "alphanumeric", "parent", null))
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral") { Id = targetId })
            .WithComponent(component)
            .Build();

        var result = Preview(snapshot);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "VAL-NOM-COLLISION");
        Assert.Null(issue.TargetId);
    }

    [Fact]
    public void PreviewMatchesEveryAzureNameInThePilotReference()
    {
        var devId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var prdId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var sharedId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var environments = new[]
        {
            new EnvironmentSnapshot("dev", "francecentral") { Id = devId, Position = 1 },
            new EnvironmentSnapshot("prd", "uksouth") { Id = prdId, Position = 2 }
        };
        var components = new[]
        {
            ReferenceComponent("core", "PerEnvironment", [devId, prdId], null,
                ("LogAnalyticsWorkspace", "main"), ("ApplicationInsights", "main"), ("KeyVault", "main")),
            ReferenceComponent("data", "PerEnvironment", [devId, prdId], null,
                ("SqlServer", "orders"), ("SqlDatabase", "orders")),
            ReferenceComponent("platform", "Single", [], new ComponentSingleTargetSnapshot(
                    sharedId, "shared", Guid.NewGuid(), "francecentral", "local", false, [], []),
                ("ContainerRegistry", "main")),
            ReferenceComponent("orders", "PerEnvironment", [devId, prdId], null,
                ("UserAssignedIdentity", "api"), ("ContainerAppsEnvironment", "main"), ("ContainerApp", "api"))
        };
        var snapshot = new ModelSnapshotBuilder()
            .WithNaming(Compact())
            .WithEnvironment(environments[0])
            .WithEnvironment(environments[1])
            .WithComponent(components[0])
            .WithComponent(components[1])
            .WithComponent(components[2])
            .WithComponent(components[3])
            .Build();

        var result = Preview(snapshot);

        Assert.Empty(result.Issues);
        var expectedResources = new[]
        {
            ("LogAnalyticsWorkspace", "main", "dev", "log-shop-main-dev"),
            ("LogAnalyticsWorkspace", "main", "prd", "log-shop-main-prd"),
            ("ApplicationInsights", "main", "dev", "appi-shop-main-dev"),
            ("ApplicationInsights", "main", "prd", "appi-shop-main-prd"),
            ("KeyVault", "main", "dev", "kv-shop-main-dev"),
            ("KeyVault", "main", "prd", "kv-shop-main-prd"),
            ("SqlServer", "orders", "dev", "sql-shop-orders-dev"),
            ("SqlServer", "orders", "prd", "sql-shop-orders-prd"),
            ("SqlDatabase", "orders", "dev", "sqldb-shop-orders-dev"),
            ("SqlDatabase", "orders", "prd", "sqldb-shop-orders-prd"),
            ("ContainerRegistry", "main", "shared", "crshopmainshared"),
            ("UserAssignedIdentity", "api", "dev", "id-shop-api-dev"),
            ("UserAssignedIdentity", "api", "prd", "id-shop-api-prd"),
            ("ContainerAppsEnvironment", "main", "dev", "cae-shop-main-dev"),
            ("ContainerAppsEnvironment", "main", "prd", "cae-shop-main-prd"),
            ("ContainerApp", "api", "dev", "ca-shop-api-dev"),
            ("ContainerApp", "api", "prd", "ca-shop-api-prd")
        };
        var actualResources = result.Items
            .Select(item => (item.ResourceType, item.LogicalName, item.EnvironmentCode, item.ResourceName))
            .OrderBy(item => item.ResourceType, StringComparer.Ordinal)
            .ThenBy(item => item.LogicalName, StringComparer.Ordinal)
            .ThenBy(item => item.EnvironmentCode, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedResources
            .OrderBy(item => item.Item1, StringComparer.Ordinal)
            .ThenBy(item => item.Item2, StringComparer.Ordinal)
            .ThenBy(item => item.Item3, StringComparer.Ordinal), actualResources);

        var expectedGroups = PilotResourceGroupNames.Order(StringComparer.Ordinal);
        var actualGroups = result.Items.Select(item => item.ResourceGroupName).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        Assert.Equal(expectedGroups, actualGroups);
    }

    private static ResourceSnapshot Resource(Guid resourceGroupId, string logicalName, Guid targetId) => new(
        Guid.NewGuid(),
        resourceGroupId,
        "KeyVault",
        logicalName,
        string.Empty,
        false,
        new Dictionary<string, System.Text.Json.JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        [])
    {
        Presences = new Dictionary<Guid, bool> { [targetId] = true }
    };

    private static ComponentSnapshot Component(
        Guid targetId,
        Guid resourceGroupId,
        string code,
        IReadOnlyList<ResourceSnapshot> resources) => new(
        Guid.NewGuid(),
        code,
        code,
        "PerEnvironment",
        [targetId],
        null,
        [new ResourceGroupSnapshot(resourceGroupId, "shared", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
        resources);

    private static ComponentSnapshot ReferenceComponent(
        string code,
        string mode,
        IReadOnlyList<Guid> environmentIds,
        ComponentSingleTargetSnapshot? singleTarget,
        params (string Type, string Name)[] resources)
    {
        var groupId = Guid.NewGuid();
        var targetIds = mode == "Single" ? [singleTarget!.Id] : environmentIds;
        var resourceSnapshots = resources.Select(resource => Resource(groupId, resource.Name, targetIds[0]) with
        {
            Type = resource.Type,
            Presences = new Dictionary<Guid, bool>()
        }).ToArray();
        return new ComponentSnapshot(
            Guid.NewGuid(),
            code,
            code,
            mode,
            environmentIds,
            singleTarget,
            [new ResourceGroupSnapshot(groupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            resourceSnapshots)
        {
            TargetsAllEnvironments = mode == "PerEnvironment"
        };
    }

    private static ModelSnapshot CreateSnapshot(NamingConventionSnapshot naming, string region = "francecentral") => new(
        new ProjectSnapshot("Boutique en ligne", "shop", string.Empty),
        [new EnvironmentSnapshot("dev", region)
        {
            Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Name = "Development"
        }],
        naming,
        new Dictionary<string, string> { ["costCenter"] = "commerce" },
        [CreateDefaultComponent()])
    {
        NamingRules = DefaultNamingRules(),
        RegionCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["francecentral"] = "frc",
            ["uksouth"] = "uks"
        }
    };

    private static ComponentSnapshot CreateDefaultComponent()
    {
        var environmentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var resourceGroupId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        return new ComponentSnapshot(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            "Main",
            "main",
            "PerEnvironment",
            [environmentId],
            null,
            [new ResourceGroupSnapshot(resourceGroupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
            [Resource(resourceGroupId, "main", environmentId)]);
    }

    private static Dictionary<string, NamingRuleSnapshot> DefaultNamingRules() =>
        new Dictionary<string, NamingRuleSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["KeyVault"] = new("kv", "^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$", "lower", ["-"], true, 3, 24, "letter", "alphanumeric", "global", "vault.azure.net"),
            ["ContainerRegistry"] = new("cr", "^[A-Za-z0-9]{5,50}$", "mixed", [], true, 5, 50, "alphanumeric", "alphanumeric", "global", "azurecr.io"),
            [BuiltInNamingRules.ResourceGroupType] = BuiltInNamingRules.ResourceGroup
        };

    private static NamingConventionSnapshot Compact() => new(
        "Compact",
        "{abbr}-{project}-{name}-{env}",
        "rg-{project}-{component}-{name}-{env}");

    private static NamingConventionSnapshot Caf() => new(
        "CAF",
        "{abbr}-{project}-{name}-{env}-{region}",
        "rg-{project}-{component}-{name}-{env}-{region}");

    private static NamingPreviewResult Preview(ModelSnapshot snapshot) => NamingEngine.Preview(snapshot, LinkKinds);
}
