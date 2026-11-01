using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;
using InfraFlowSculptor.Engine.Validation.Rules;
using InfraFlowSculptor.Engine.Values;

namespace InfraFlowSculptor.Engine.Tests.Validation;

public sealed class ValidationEngineTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.3")!;
    private static readonly CatalogVersion CatalogWithSqlAdministrator = CatalogLoader.Load().GetVersion("2026.10-pilot.4")!;
    private static readonly CatalogVersion CatalogWithResourceConstraints = CatalogLoader.Load().GetVersion("2026.10-pilot.5")!;

    [Fact]
    public void ReferenceProjectProducesOnlyExternalScopeInfoAndSqlAdminWarning()
    {
        var fixture = ReferenceProject();

        var result = CreateEngine().Evaluate(fixture.Snapshot, Catalog);

        Assert.Equal(
            ["VAL-LIA-SQL-ADMIN", "VAL-LIA-PORTEE-EXTERNE"],
            result.Findings.Select(finding => finding.Code));
        Assert.Equal(1, result.Findings.Count(finding => finding.Severity == FindingSeverity.Warning));
        Assert.Equal(1, result.Findings.Count(finding => finding.Severity == FindingSeverity.Info));
        Assert.DoesNotContain(result.Findings, finding => finding.Severity == FindingSeverity.Error);
        Assert.DoesNotContain("''s", result.Findings.Single(finding => finding.Code == "VAL-LIA-PORTEE-EXTERNE").MessageEn);
    }

    [Fact]
    public void SqlAdministratorFindingKeepsLinksForEveryDatabaseOnTheServer()
    {
        var fixture = ReferenceProject();
        var data = fixture.Snapshot.Components.Single(component => component.Code == "data");
        var orders = fixture.Snapshot.Components.Single(component => component.Code == "orders");
        var sqlServer = data.Resources.Single(resource => resource.Type == "SqlServer");
        var application = orders.Resources.Single(resource => resource.Type == "ContainerApp");
        var secondDatabase = Resource("SqlDatabase", "sqldb audit", sqlServer.ResourceGroupId);
        var dataWithSecondDatabase = data with { Resources = data.Resources.Append(secondDatabase).ToArray() };
        var secondHostingLink = Link(data.Id, secondDatabase.Id, data.Id, sqlServer.Id, LinkKind.Hosting);
        var secondDataAccessLink = Link(
            orders.Id,
            application.Id,
            data.Id,
            secondDatabase.Id,
            LinkKind.DataAccess,
            LinkIdentityKind.System,
            dataAccessLevel: DataAccessLevel.Lecture);
        var snapshot = fixture.Snapshot with
        {
            Components = fixture.Snapshot.Components
                .Select(component => component.Id == data.Id ? dataWithSecondDatabase : component)
                .ToArray(),
            Links = fixture.Snapshot.Links.Concat([secondHostingLink, secondDataAccessLink]).ToArray()
        };

        var result = CreateEngine([new SqlAdministratorMembershipRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(sqlServer.Id, finding.Subject.Id);
        Assert.Contains(secondHostingLink.Id, finding.RelatedLinkIds);
        Assert.Contains(secondDataAccessLink.Id, finding.RelatedLinkIds);
        Assert.Contains("sqldb audit", finding.MessageFr);
    }

    [Fact]
    public void SqlAdministratorFindingVersionTracksProjectContextChanges()
    {
        var fixture = ReferenceProject();
        var snapshot = fixture.Snapshot with
        {
            Project = fixture.Snapshot.Project with { ModelVersion = 17 }
        };

        var result = CreateEngine([new SqlAdministratorMembershipRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal(18, Assert.Single(result.Findings).ObjectVersion);
    }

    [Fact]
    public void SqlAdministratorFindingVersionDoesNotOverflowAtMaximumModelVersion()
    {
        var fixture = ReferenceProject();
        var snapshot = fixture.Snapshot with
        {
            Project = fixture.Snapshot.Project with { ModelVersion = int.MaxValue }
        };

        var result = CreateEngine([new SqlAdministratorMembershipRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal(int.MaxValue, Assert.Single(result.Findings).ObjectVersion);
    }

    [Fact]
    public void SharedTargetInTheSameSubscriptionDoesNotProduceExternalScopeFinding()
    {
        var fixture = ReferenceProject();
        var subscriptionId = fixture.Snapshot.Environments[0].SubscriptionId;
        var platform = fixture.Snapshot.Components.Single(component => component.Code == "platform");
        var sameScopePlatform = platform with
        {
            SingleTarget = platform.SingleTarget! with { SubscriptionId = subscriptionId }
        };
        var snapshot = fixture.Snapshot with
        {
            Components = fixture.Snapshot.Components
                .Select(component => component.Id == platform.Id ? sameScopePlatform : component)
                .ToArray()
        };

        var result = CreateEngine([new ExternalLinkScopeRule()]).Evaluate(snapshot, Catalog);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void AllEnvironmentComponentUsesProjectEnvironmentSubscriptionsForExternalScope()
    {
        var fixture = ReferenceProject();
        var orders = fixture.Snapshot.Components.Single(component => component.Code == "orders") with
        {
            TargetEnvironmentIds = [],
            TargetsAllEnvironments = true
        };
        var snapshot = fixture.Snapshot with
        {
            Components = fixture.Snapshot.Components
                .Select(component => component.Id == orders.Id ? orders : component)
                .ToArray()
        };

        var result = CreateEngine([new ExternalLinkScopeRule()]).Evaluate(snapshot, Catalog);

        Assert.Contains(result.Findings, finding => finding.Code == "VAL-LIA-PORTEE-EXTERNE");
    }

    [Fact]
    public void MissingUniqueHostingLinkProducesFixableError()
    {
        var fixture = ReferenceProject();
        var snapshot = fixture.Snapshot with
        {
            Links = fixture.Snapshot.Links.Where(link => link.Kind != LinkKind.Hosting
                || link.SourceResourceId != fixture.ContainerAppId).ToArray()
        };

        var result = CreateEngine([new RequiredLinkRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-LIA-OBLIGATOIRE", finding.Code);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal(fixture.ContainerAppId, finding.Subject.Id);
        Assert.Equal("VAL-LIA-OBLIGATOIRE", finding.FixCode);
    }

    [Fact]
    public void RequiredSqlHostingLinkDoesNotOfferAnIncompatibleCrossComponentFix()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var databaseComponentId = Guid.NewGuid();
        var serverComponentId = Guid.NewGuid();
        var database = Resource("SqlDatabase", "orders", Guid.NewGuid());
        var localServer = Resource("SqlServer", "local-server", database.ResourceGroupId);
        var server = Resource("SqlServer", "orders-server", Guid.NewGuid());
        var secondServer = Resource("SqlServer", "orders-server-secondary", Guid.NewGuid());
        var databaseComponent = Component(databaseComponentId, "data", "PerEnvironment", [database, localServer], [devId, prdId]);
        var serverComponent = Component(serverComponentId, "platform", "PerEnvironment", [server, secondServer], [devId, prdId]);
        var firstInvalidLink = Link(databaseComponentId, database.Id, serverComponentId, server.Id, LinkKind.Hosting);
        var secondInvalidLink = Link(databaseComponentId, database.Id, serverComponentId, secondServer.Id, LinkKind.Hosting);
        var snapshot = Snapshot(
            [databaseComponent, serverComponent],
            [firstInvalidLink, secondInvalidLink],
            [
                new EnvironmentSnapshot("dev", "francecentral") { Id = devId, SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") },
                new EnvironmentSnapshot("prd", "francecentral") { Id = prdId, SubscriptionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") }
            ]);

        var result = CreateEngine().Evaluate(snapshot, Catalog);

        var requiredFinding = Assert.Single(result.Findings, finding =>
            finding.Code == "VAL-LIA-OBLIGATOIRE" && finding.Subject.Id == database.Id);
        Assert.Null(requiredFinding.FixCode);
        var scopeFinding = Assert.Single(result.Findings, finding =>
            finding.Code == "VAL-LIA-COMPOSANT" && finding.Subject.Id == database.Id);
        Assert.Equal(FindingSeverity.Error, scopeFinding.Severity);
        Assert.Contains("même composant", scopeFinding.MessageFr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same component", scopeFinding.MessageEn, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            new[] { firstInvalidLink.Id, secondInvalidLink.Id }.Order(),
            scopeFinding.RelatedLinkIds);
    }

    [Fact]
    public void RequiredLinkRuleSkipsAnIssueWhoseResourceIsMissingFromTheSnapshot()
    {
        var snapshot = Snapshot([], []);
        var issue = new WiringIssue(
            "VAL-LIA-OBLIGATOIRE",
            "Missing required link.",
            Guid.NewGuid(),
            Guid.NewGuid())
        {
            Kind = LinkKind.Hosting
        };
        var context = Context(snapshot, new WiringResult([], [], [], [], [], [], [issue]));

        var findings = new RequiredLinkRule().Evaluate(context);

        Assert.Empty(findings);
    }

    [Fact]
    public void ProjectWithoutEnvironmentProducesBlockingFinding()
    {
        var snapshot = Snapshot([], []);

        var result = CreateEngine([new ProjectEnvironmentRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-ENV-AUCUN", finding.Code);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void ProtectedEnvironmentWithoutApproverProducesFindingOnEnvironment()
    {
        var environmentId = Guid.NewGuid();
        var snapshot = Snapshot([], [], [new EnvironmentSnapshot("prd", "francecentral")
        {
            Id = environmentId,
            Name = "Production",
            IsProtected = true
        }]);

        var result = CreateEngine([new ProtectedEnvironmentApproverRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-ENV-APPROBATEURS", finding.Code);
        Assert.Equal(environmentId, finding.Subject.Id);
        Assert.Equal(environmentId, finding.EnvironmentId);
    }

    [Fact]
    public void PerEnvironmentComponentWithoutTargetProducesFinding()
    {
        var component = Component(Guid.NewGuid(), "empty", "PerEnvironment", []);
        component = component with { TargetsAllEnvironments = false };
        var snapshot = Snapshot([component], []);

        var result = CreateEngine([new ComponentWithoutEnvironmentRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-CMP-SANS-ENVIRONNEMENT", finding.Code);
        Assert.Equal(component.Id, finding.Subject.Id);
    }

    [Fact]
    public void SingleComponentCannotDependOnPerEnvironmentComponent()
    {
        var sharedId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var sharedResource = Resource("LogAnalyticsWorkspace", "shared", Guid.NewGuid());
        var environmentResource = Resource("ApplicationInsights", "dev", Guid.NewGuid());
        var link = Link(sharedId, sharedResource.Id, environmentId, environmentResource.Id, LinkKind.Logging);
        var snapshot = Snapshot(
        [
            Component(sharedId, "shared", "Single", [sharedResource], singleTargetId: Guid.NewGuid()),
            Component(environmentId, "environment", "PerEnvironment", [environmentResource], [Guid.NewGuid()])
        ],
        [link]);

        var result = CreateEngine([new ComponentDirectionRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-CMP-SENS", finding.Code);
        Assert.Equal(link.Id, finding.Subject.Id);
    }

    [Fact]
    public void ResourceConstraintFindingIncludesEnvironment()
    {
        using var cpuValue = JsonDocument.Parse("0.1");
        var environmentId = Guid.NewGuid();
        var resource = Resource("ContainerApp", "api", Guid.NewGuid()) with
        {
            Properties = new Dictionary<string, JsonElement> { ["cpu"] = cpuValue.RootElement.Clone() }
        };
        var component = Component(Guid.NewGuid(), "api", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-RES-CONTRAINTE", finding.Code);
        Assert.Equal(resource.Id, finding.Subject.Id);
        Assert.Equal(environmentId, finding.EnvironmentId);
    }

    [Fact]
    public void ResourceConstraintRuleChecksReplicaRangeForEachEffectiveEnvironment()
    {
        using var invalidMinimum = JsonDocument.Parse("11");
        using var validMaximum = JsonDocument.Parse("10");
        var fixture = ReferenceProject();
        var productionId = fixture.Snapshot.Environments.Single(environment => environment.Code == "prd").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [productionId] = new Dictionary<string, JsonElement>
            {
                ["minReplicas"] = invalidMinimum.RootElement.Clone(),
                ["maxReplicas"] = validMaximum.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);
        var finding = Assert.Single(result.Findings, value => value.Code == "VAL-RES-CONTRAINTE"
            && value.Subject.Id == app.Id);

        Assert.Equal(productionId, finding.EnvironmentId);
        Assert.Equal("constraint-replica-range", finding.DetailKey);
        Assert.Contains("must be less than or equal to", finding.MessageEn, StringComparison.Ordinal);
        Assert.DoesNotContain("doit être inférieur ou égal à", finding.MessageEn, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceConstraintRuleAcceptsNumericEnumValuesDeclaredByLogAnalyticsDescriptor()
    {
        using var capacityReservationName = JsonDocument.Parse("\"CapacityReservation\"");
        using var capacityReservationLevel = JsonDocument.Parse("100");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var workspace = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "LogAnalyticsWorkspace");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(workspace.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["sku"] = capacityReservationName.RootElement.Clone(),
                ["capacityReservationLevel"] = capacityReservationLevel.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, workspace with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);

        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == workspace.Id && finding.EnvironmentId == developmentId);
    }

    [Fact]
    public void ResourceConstraintRuleAppliesAllowedTuplesOnlyWhenTheProfileConditionMatches()
    {
        using var cpu = JsonDocument.Parse("1");
        using var memory = JsonDocument.Parse("\"1Gi\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["cpu"] = cpu.RootElement.Clone(),
                ["memoryGi"] = memory.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);

        var finding = Assert.Single(result.Findings, value => value.Code == "VAL-RES-CONTRAINTE"
            && value.Subject.Id == app.Id);
        Assert.Equal(developmentId, finding.EnvironmentId);
        Assert.Equal("constraint-consumption-cpu-memory", finding.DetailKey);
    }

    [Fact]
    public void ResourceConstraintRuleDoesNotThrowWhenAComparedEffectiveValueHasTheWrongType()
    {
        using var invalidReplicaCount = JsonDocument.Parse("\"two\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["minReplicas"] = invalidReplicaCount.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);

        Assert.Contains(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == app.Id
            && finding.EnvironmentId == developmentId
            && finding.MessageEn.Contains("minReplicas", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == app.Id
            && finding.EnvironmentId == developmentId
            && finding.DetailKey == "constraint-replica-range");
        Assert.False(result.Readiness.ModelValid);
    }

    [Fact]
    public void ResourceConstraintRuleAssignsDistinctKeysToInvalidEffectiveProperties()
    {
        using var invalidCpu = JsonDocument.Parse("\"fast\"");
        using var invalidMemory = JsonDocument.Parse("\"large\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["cpu"] = invalidCpu.RootElement.Clone(),
                ["memoryGi"] = invalidMemory.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var valueFindings = CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints)
            .Findings
            .Where(finding => finding.Code == "VAL-RES-CONTRAINTE"
                && finding.Subject.Id == app.Id
                && finding.EnvironmentId == developmentId
                && finding.MessageEn.Contains("violates the catalog", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, valueFindings.Length);
        Assert.Equal(["value-cpu", "value-memoryGi"], valueFindings.Select(finding => finding.DetailKey).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ResourceConstraintRuleSanitizesUntrustedUnknownMemberNamesInFindings()
    {
        using var malformedProfiles = JsonDocument.Parse("""
            [{"bad\u202Ekey":true,"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = malformedProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var finding = Assert.Single(CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints)
            .Findings,
            value => value.Code == "VAL-RES-CONTRAINTE"
                && value.Subject.Id == environment.Id
                && value.EnvironmentId == developmentId);

        Assert.DoesNotContain('\u202E', finding.MessageEn);
        Assert.Contains('�', finding.MessageEn);
    }

    [Fact]
    public void ResourceConstraintRuleAcceptsAProfileDeclaredByTheLinkedEnvironment()
    {
        using var profiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4}]
            """);
        using var profileName = JsonDocument.Parse("\"D4\"");
        using var cpu = JsonDocument.Parse("1");
        using var memory = JsonDocument.Parse("\"1Gi\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var environmentOverrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = profiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = environmentOverrides });
        var appOverrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfileName"] = profileName.RootElement.Clone(),
                ["cpu"] = cpu.RootElement.Clone(),
                ["memoryGi"] = memory.RootElement.Clone()
            }
        };
        var appWithProfile = FindResource(snapshot, app.Id) with
        {
            Overrides = appOverrides
        };
        snapshot = ReplaceResource(snapshot, appWithProfile);

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);

        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == app.Id);
    }

    [Fact]
    public void ResourceConstraintRuleDoesNotRejectAnUnverifiedProfileOnAnExistingEnvironment()
    {
        using var profileName = JsonDocument.Parse("\"D4\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var existingIds = new Dictionary<Guid, string>(environment.ExistingIds)
        {
            [developmentId] = "/subscriptions/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/resourceGroups/rg-shop-orders-dev/providers/Microsoft.App/managedEnvironments/cae-shop"
        };
        environment = environment with { IsExisting = true, ExistingIds = existingIds };
        var snapshot = ReplaceResource(fixture.Snapshot, environment);
        var app = FindResource(snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfileName"] = profileName.RootElement.Clone()
            }
        };
        snapshot = ReplaceResource(snapshot, app with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);

        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == app.Id
            && finding.DetailKey == "allowed-from-workloadProfileName");
    }

    [Fact]
    public void ResourceConstraintRuleRejectsAProfileMissingFromTheLinkedEnvironment()
    {
        using var unknownProfile = JsonDocument.Parse("\"D8\"");
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfileName"] = unknownProfile.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);
        var finding = Assert.Single(result.Findings, value => value.Code == "VAL-RES-CONTRAINTE"
            && value.Subject.Id == app.Id);

        Assert.Equal(developmentId, finding.EnvironmentId);
        Assert.Equal("allowed-from-workloadProfileName", finding.DetailKey);
    }

    [Fact]
    public void ResourceConstraintRuleSanitizesAndBoundsUnknownLinkedProfileNames()
    {
        var unknownProfileName = "D\u202E" + new string('X', 96);
        using var unknownProfile = JsonDocument.Parse(JsonSerializer.Serialize(unknownProfileName));
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var app = FindResource(fixture.Snapshot, fixture.ContainerAppId);
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(app.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfileName"] = unknownProfile.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, app with { Overrides = overrides });

        var finding = Assert.Single(CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints)
            .Findings,
            value => value.Code == "VAL-RES-CONTRAINTE"
                && value.Subject.Id == app.Id
                && value.EnvironmentId == developmentId
                && value.DetailKey == "allowed-from-workloadProfileName");

        Assert.DoesNotContain('\u202E', finding.MessageEn);
        Assert.DoesNotContain(unknownProfileName, finding.MessageEn, StringComparison.Ordinal);
        Assert.True(finding.MessageEn.Length < 300);
    }

    [Fact]
    public void ResourceConstraintRuleChecksWorkloadProfileInstanceRangePerEffectiveEnvironment()
    {
        using var invalidProfiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = invalidProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);
        var finding = Assert.Single(result.Findings, value => value.Code == "VAL-RES-CONTRAINTE"
            && value.Subject.Id == environment.Id);

        Assert.Equal(developmentId, finding.EnvironmentId);
        Assert.Matches("^constraint-workload-profile-instance-range:item-[a-f0-9]{64}$", finding.DetailKey);
        Assert.Contains("name = D4", finding.MessageEn, StringComparison.Ordinal);
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("workloadProfiles\0name\0string\0D4")))
            .ToLowerInvariant();
        Assert.EndsWith($"item-{expectedHash}", finding.DetailKey, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceConstraintRuleIdentifiesInvalidArrayItemsWithStableUniqueKeys()
    {
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");

        ValidationResult Evaluate(string profilesJson)
        {
            using var profiles = JsonDocument.Parse(profilesJson);
            var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
            {
                [developmentId] = new Dictionary<string, JsonElement>
                {
                    ["workloadProfiles"] = profiles.RootElement.Clone()
                }
            };
            var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });
            return CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);
        }

        const string orderedProfiles = """
            [
              {"name":"D4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2},
              {"name":"E4","workloadProfileType":"E4","minimumCount":6,"maximumCount":3}
            ]
            """;
        const string reversedProfiles = """
            [
              {"name":"E4","workloadProfileType":"E4","minimumCount":6,"maximumCount":3},
              {"name":"D4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2}
            ]
            """;

        var orderedFindings = Evaluate(orderedProfiles).Findings
            .Where(finding => finding.Code == "VAL-RES-CONTRAINTE"
                && finding.Subject.Id == environment.Id
                && finding.EnvironmentId == developmentId)
            .OrderBy(finding => finding.MessageEn, StringComparer.Ordinal)
            .ToArray();
        var reversedFindings = Evaluate(reversedProfiles).Findings
            .Where(finding => finding.Code == "VAL-RES-CONTRAINTE"
                && finding.Subject.Id == environment.Id
                && finding.EnvironmentId == developmentId)
            .ToArray();

        Assert.Equal(2, orderedFindings.Length);
        Assert.Contains(orderedFindings, finding => finding.MessageEn.Contains("D4", StringComparison.Ordinal));
        Assert.Contains(orderedFindings, finding => finding.MessageEn.Contains("E4", StringComparison.Ordinal));
        Assert.All(orderedFindings, finding => Assert.True(finding.DetailKey?.Length <= 128));
        Assert.Equal(orderedFindings.Select(finding => finding.DetailKey).Order(StringComparer.Ordinal),
            reversedFindings.Select(finding => finding.DetailKey).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ResourceConstraintRuleDoesNotThrowWhenAnArrayItemRepeatsAJsonMember()
    {
        using var malformedProfiles = JsonDocument.Parse("""
            [{"name":"D4","name":"E4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = malformedProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var exception = Record.Exception(() => CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints));

        Assert.Null(exception);
    }

    [Fact]
    public void ResourceConstraintRuleRejectsNullForRequiredTypedItemMembers()
    {
        using var malformedProfiles = JsonDocument.Parse("""
            [{"name":null,"workloadProfileType":"D4","minimumCount":null,"maximumCount":4}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = malformedProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var finding = Assert.Single(CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints)
            .Findings,
            value => value.Code == "VAL-RES-CONTRAINTE"
                && value.Subject.Id == environment.Id
                && value.EnvironmentId == developmentId);

        Assert.Contains("required member 'name' is missing or null", finding.MessageEn, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceConstraintRuleSkipsPerItemConstraintsWhenUniqueKeysAreDuplicated()
    {
        using var duplicateProfiles = JsonDocument.Parse("""
            [
              {"name":"D4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2},
              {"name":"D4","workloadProfileType":"D4","minimumCount":6,"maximumCount":3}
            ]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = duplicateProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var findings = CreateEngine([new ResourceConstraintRule()])
            .Evaluate(snapshot, CatalogWithResourceConstraints)
            .Findings
            .Where(finding => finding.Code == "VAL-RES-CONTRAINTE"
                && finding.Subject.Id == environment.Id
                && finding.EnvironmentId == developmentId)
            .ToArray();

        var finding = Assert.Single(findings);
        Assert.Contains("share the same", finding.MessageEn, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceConstraintRuleReplacesUnicodeFormatCharactersInItemLabels()
    {
        using var malformedProfiles = JsonDocument.Parse("""
            [{"name":"D\u202E4","workloadProfileType":"D4","minimumCount":5,"maximumCount":2}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = malformedProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var finding = Assert.Single(
            CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints).Findings,
            value => value.Code == "VAL-RES-CONTRAINTE"
                && value.Subject.Id == environment.Id
                && value.EnvironmentId == developmentId);

        Assert.DoesNotContain('\u202E', finding.MessageEn);
        Assert.Contains('�', finding.MessageEn);
    }

    [Fact]
    public void ResourceConstraintRuleReportsMalformedTypedWorkloadProfileItems()
    {
        using var malformedProfiles = JsonDocument.Parse("""
            [{"name":"D4","workloadProfileType":"D4","minimumCount":0,"maximumCount":4,"unsupported":true}]
            """);
        var fixture = ReferenceProject();
        var developmentId = fixture.Snapshot.Environments.Single(environment => environment.Code == "dev").Id;
        var environment = fixture.Snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Type == "ContainerAppsEnvironment");
        var overrides = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(environment.Overrides)
        {
            [developmentId] = new Dictionary<string, JsonElement>
            {
                ["workloadProfiles"] = malformedProfiles.RootElement.Clone()
            }
        };
        var snapshot = ReplaceResource(fixture.Snapshot, environment with { Overrides = overrides });

        var result = CreateEngine([new ResourceConstraintRule()]).Evaluate(snapshot, CatalogWithResourceConstraints);
        var finding = Assert.Single(result.Findings, value => value.Code == "VAL-RES-CONTRAINTE"
            && value.Subject.Id == environment.Id);

        Assert.Equal(developmentId, finding.EnvironmentId);
    }

    [Fact]
    public void UnresolvedCatalogConditionWithConfiguredValueBlocksValidation()
    {
        using var enabled = JsonDocument.Parse("true");
        var environmentId = Guid.NewGuid();
        var resource = Resource("ContainerAppsEnvironment", "main", Guid.NewGuid()) with
        {
            Properties = new Dictionary<string, JsonElement> { ["zoneRedundant"] = enabled.RootElement.Clone() }
        };
        var component = Component(Guid.NewGuid(), "orders", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine().Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-RES-CONTRAINTE", finding.Code);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        var conditionKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "zoneRedundant\0virtualNetworkConfiguration"))).ToLowerInvariant();
        Assert.Equal($"condition-{conditionKeyHash}", finding.DetailKey);
        Assert.False(result.Readiness.ModelValid);
    }

    [Fact]
    public void UnresolvedCatalogConditionWithDefaultValueDoesNotBlockValidation()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("ContainerAppsEnvironment", "main", Guid.NewGuid());
        var component = Component(Guid.NewGuid(), "orders", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine().Evaluate(snapshot, Catalog);

        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == resource.Id
            && finding.EnvironmentId == environmentId);
        Assert.True(result.Readiness.ModelValid);
    }

    [Fact]
    public void UnresolvedExposureConditionBlocksEvenWhenItsDefaultIsUsed()
    {
        var method = typeof(ResourceConstraintRule).GetMethod(
            "ShouldReportUnresolvedCondition",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var issue = new EffectiveValueConditionIssue("exposure", "unknown", false);

        Assert.True((bool)method.Invoke(null, [issue])!);
    }

    [Fact]
    public void PresentExistingResourceRequiresAzureId()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "external", Guid.NewGuid()) with
        {
            IsExisting = true,
            Presences = new Dictionary<Guid, bool> { [environmentId] = true }
        };
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new ExistingResourceIdRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-RES-EXISTANTE-ID", finding.Code);
        Assert.Equal(environmentId, finding.EnvironmentId);
    }

    [Fact]
    public void ResourceAbsentFromEveryTargetProducesWarning()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "unused", Guid.NewGuid()) with
        {
            Presences = new Dictionary<Guid, bool> { [environmentId] = false }
        };
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new ResourceAbsentEverywhereRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-RES-ABSENTE-PARTOUT", finding.Code);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void EffectiveTagLimitCountsInheritedTags()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "main", Guid.NewGuid()) with
        {
            Tags = new Dictionary<string, string> { ["resource"] = "tag" }
        };
        var componentTags = Enumerable.Range(0, 25).ToDictionary(index => $"component-{index}", index => index.ToString());
        var projectTags = Enumerable.Range(0, 25).ToDictionary(index => $"project-{index}", index => index.ToString());
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]) with { Tags = componentTags };
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]) with { Tags = projectTags };

        var result = CreateEngine([new ProjectTagsRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PRJ-TAGS", finding.Code);
        Assert.Equal(environmentId, finding.EnvironmentId);
    }

    [Fact]
    public void EffectiveTagLimitIncludesSystemTags()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "main", Guid.NewGuid());
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]);
        var projectTags = Enumerable.Range(0, 47).ToDictionary(index => $"project-{index}", index => index.ToString());
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]) with { Tags = projectTags };

        var result = CreateEngine([new ProjectTagsRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PRJ-TAGS", finding.Code);
        Assert.Contains("51", finding.MessageFr);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"\"]")]
    [InlineData("[\"   \"]")]
    public void RestrictedSqlExposureWithoutUsableRangesProducesError(string rangesJson)
    {
        using var exposure = JsonDocument.Parse("\"Restricted\"");
        using var ranges = JsonDocument.Parse(rangesJson);
        var environmentId = Guid.NewGuid();
        var resource = Resource("SqlServer", "sql", Guid.NewGuid()) with
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["exposure"] = exposure.RootElement.Clone(),
                ["allowedIpRanges"] = ranges.RootElement.Clone()
            }
        };
        var component = Component(Guid.NewGuid(), "data", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new RestrictedExposureRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-NET-IP-VIDE", finding.Code);
        Assert.Equal(environmentId, finding.EnvironmentId);
        Assert.Null(finding.FixCode);
    }

    [Fact]
    public void UnsupportedExposureModeIsRejectedByCatalogConstraint()
    {
        using var exposure = JsonDocument.Parse("\"Unsupported\"");
        var environmentId = Guid.NewGuid();
        var resource = Resource("SqlServer", "sql", Guid.NewGuid()) with
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["exposure"] = exposure.RootElement.Clone()
            }
        };
        var component = Component(Guid.NewGuid(), "data", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine().Evaluate(snapshot, Catalog);

        Assert.Contains(result.Findings, finding => finding.Code == "VAL-RES-CONTRAINTE"
            && finding.Subject.Id == resource.Id
            && finding.EnvironmentId == environmentId);
        Assert.DoesNotContain(result.Findings, finding => finding.Code == "VAL-NET-IP-VIDE");
        Assert.False(result.Readiness.ModelValid);
    }

    [Fact]
    public void CatalogRegexValidationDoesNotThrowForInvalidPattern()
    {
        using var value = JsonDocument.Parse("\"value\"");
        using var definition = JsonDocument.Parse("{\"kind\":\"string\",\"pattern\":\"(\"}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Equal(CatalogPropertyValueFailureKind.Pattern, failure?.Kind);
    }

    [Theory]
    [InlineData("[42]", "{\"kind\":\"array\",\"items\":{\"kind\":\"string\"}}")]
    [InlineData("[null]", "{\"kind\":\"array\",\"items\":{\"kind\":\"string\"}}")]
    [InlineData("[\"203.0.113.01/24\"]", "{\"kind\":\"array\",\"items\":{\"kind\":\"string\",\"format\":\"ipv4Cidr\"}}")]
    [InlineData("[\"203.0.113.1/24\"]", "{\"kind\":\"array\",\"items\":{\"kind\":\"string\",\"format\":\"ipv4Cidr\"}}")]
    public void CatalogArrayConstraintsValidateItemTypeAndFormat(string valueJson, string definitionJson)
    {
        using var value = JsonDocument.Parse(valueJson);
        using var definition = JsonDocument.Parse(definitionJson);

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
    }

    [Fact]
    public void CatalogArrayConstraintsAcceptCanonicalIpv4CidrItems()
    {
        using var value = JsonDocument.Parse("[\"203.0.113.0/24\"]");
        using var definition = JsonDocument.Parse("{\"kind\":\"array\",\"items\":{\"kind\":\"string\",\"format\":\"ipv4Cidr\"}}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Null(failure);
    }

    [Fact]
    public void CatalogArrayConstraintsAcceptAFullIpv4RangeAsAValidCidr()
    {
        using var value = JsonDocument.Parse("[\"0.0.0.0/0\"]");
        using var definition = JsonDocument.Parse("{\"kind\":\"array\",\"items\":{\"kind\":\"string\",\"format\":\"ipv4Cidr\"}}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Null(failure);
    }

    [Fact]
    public void CatalogRegexValidationDoesNotThrowWhenPatternExceedsItsTimeLimit()
    {
        using var value = JsonDocument.Parse($"\"{new string('a', 5000)}!\"");
        using var definition = JsonDocument.Parse("{\"kind\":\"string\",\"pattern\":\"(a+)+$\"}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Equal(CatalogPropertyValueFailureKind.Pattern, failure?.Kind);
    }

    [Fact]
    public void ValidationFindingUsesTheLotDeclaredByItsRule()
    {
        var result = CreateEngine([new TestRule("VAL-TEST-LOT", "lot2")]).Evaluate(Snapshot([], []), Catalog);

        Assert.Equal("lot2", Assert.Single(result.Findings).Lot);
    }

    [Fact]
    public void FindingsAreOrderedBySeverityBeforeCode()
    {
        var result = CreateEngine([
            new TestRule("VAL-A-WARNING", "lot1", FindingSeverity.Warning),
            new TestRule("VAL-Z-ERROR", "lot1", FindingSeverity.Error)
        ]).Evaluate(Snapshot([], []), Catalog);

        Assert.Equal(
            [FindingSeverity.Error, FindingSeverity.Warning],
            result.Findings.Select(finding => finding.Severity));
    }

    [Fact]
    public void CodeRuleSourceReturnsRulesInCodeOrder()
    {
        var rules = new CodeRuleSource([new TestRule("VAL-Z", "lot1"), new TestRule("VAL-A", "lot1")]);

        Assert.Equal(["VAL-A", "VAL-Z"], rules.GetRules().Select(rule => rule.Code));
    }

    [Fact]
    public void CatalogRegionRuleRejectsUnsupportedRegion()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "main", Guid.NewGuid());
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]);
        component = component with
        {
            ResourceGroups = component.ResourceGroups.Select(group => group with { Region = "moon-1" }).ToArray()
        };
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new CatalogRegionRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal("VAL-CAT-REGION", Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void ApplicationCodeSourceErrorAtPublishDoesNotBlockModelEditingOrRevisionGeneration()
    {
        var application = CreateApplication(Guid.NewGuid(), Guid.NewGuid(), string.Empty);
        var snapshot = Snapshot([], []) with { Applications = [application] };

        var result = CreateEngine([new ApplicationCodeSourceRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal(FindingSeverity.ErrorAtPublish, Assert.Single(result.Findings).Severity);
        Assert.True(result.Readiness.ModelValid);
        Assert.True(result.Readiness.RevisionGenerable);
        Assert.False(result.Readiness.Publishable);
    }

    [Fact]
    public void ApplicationStrategyRuleRejectsUnsupportedStrategy()
    {
        var application = CreateApplication(Guid.NewGuid(), Guid.NewGuid(), "src/api") with
        {
            DeploymentStrategy = (DeploymentStrategyKind)999
        };
        var snapshot = Snapshot([], []) with { Applications = [application] };

        var result = CreateEngine([new ApplicationStrategyRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal("VAL-APP-STRATEGIE", Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void ApplicationStepRuleReportsStepMissingFromPinnedCatalog()
    {
        var step = new ApplicationStepDefinition(
            "NotInCatalog",
            true,
            true,
            ApplicationStepPosition.BeforeBuild,
            []);
        var application = CreateApplication(Guid.NewGuid(), Guid.NewGuid(), "src/api") with { Steps = [step] };
        var snapshot = Snapshot([], []) with { Applications = [application] };

        var result = CreateEngine([new ApplicationStepRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal("VAL-APP-ETAPE", Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void ResourceRequiredPropertyRuleAcceptsAConditionalCatalogDefault()
    {
        using var authentication = JsonDocument.Parse("\"EntraEtSql\"");
        var resource = Resource("SqlServer", "sql", Guid.NewGuid()) with
        {
            Properties = new Dictionary<string, JsonElement> { ["authentication"] = authentication.RootElement.Clone() }
        };
        var component = Component(Guid.NewGuid(), "data", "Single", [resource], singleTargetId: Guid.NewGuid());
        var snapshot = Snapshot([component], []);

        var result = CreateEngine([new ResourceRequiredPropertyRule()]).Evaluate(snapshot, Catalog);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void SqlServerRequiresEntraAdministratorDisplayNameAndObjectId()
    {
        var resource = Resource("SqlServer", "orders", Guid.NewGuid());
        var targetId = Guid.NewGuid();
        var component = Component(Guid.NewGuid(), "data", "Single", [resource], singleTargetId: targetId);
        var baseline = Snapshot([component], []);
        var snapshot = baseline with
        {
            Project = baseline.Project with { CatalogVersion = CatalogWithSqlAdministrator.Version }
        };

        var result = CreateEngine([new ResourceRequiredPropertyRule()]).Evaluate(snapshot, CatalogWithSqlAdministrator);

        var findings = result.Findings.Where(finding => finding.Code == "VAL-RES-REQUIS").ToArray();
        Assert.Equal(2, findings.Length);
        Assert.Contains(findings, finding => finding.MessageFr.Contains("administratorDisplayName", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.MessageFr.Contains("administratorObjectId", StringComparison.Ordinal));
        Assert.All(findings, finding => Assert.Equal(targetId, finding.EnvironmentId));
    }

    [Fact]
    public void NamingCollisionRuleReturnsNamingEngineCollisions()
    {
        var environmentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var component = Component(
            Guid.NewGuid(),
            "core",
            "PerEnvironment",
            [Resource("KeyVault", "main", groupId), Resource("KeyVault", "main", groupId)],
            [environmentId]);
        var snapshot = new InfraFlowSculptor.Engine.Tests.Builders.ModelSnapshotBuilder()
            .WithEnvironment(Environment(environmentId, "dev"))
            .WithComponent(component)
            .Build();

        var result = CreateEngine([new NamingCollisionRule()]).Evaluate(snapshot, Catalog);

        Assert.Contains(result.Findings, finding => finding.Code == "VAL-NOM-COLLISION");
    }

    [Fact]
    public void SystemTagsDisabledRuleReturnsWarningOnProject()
    {
        var initial = Snapshot([], []);
        var snapshot = initial with { Project = initial.Project with { SystemTagsEnabled = false } };

        var result = CreateEngine([new SystemTagsDisabledRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PRJ-TAGS-SYSTEME", finding.Code);
        Assert.Equal(snapshot.Project.Id, finding.Subject.Id);
    }

    [Fact]
    public void MissingLinkedResourcePresenceProvidesSafeFix()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var sourceResource = Resource("ContainerApp", "api", Guid.NewGuid());
        var targetResource = Resource("ApplicationInsights", "insights", Guid.NewGuid()) with
        {
            Presences = new Dictionary<Guid, bool> { [prdId] = false }
        };
        var source = Component(Guid.NewGuid(), "orders", "PerEnvironment", [sourceResource], [devId, prdId]);
        var target = Component(Guid.NewGuid(), "platform", "PerEnvironment", [targetResource], [devId, prdId]);
        var link = Link(source.Id, sourceResource.Id, target.Id, targetResource.Id, LinkKind.Telemetry);
        var snapshot = Snapshot([source, target], [link], [Environment(devId, "dev"), Environment(prdId, "prd")]);

        var result = CreateEngine([new ResourcePresenceRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-RES-PRESENCE", finding.Code);
        Assert.Equal(targetResource.Id, finding.Subject.Id);
        Assert.Equal(prdId, finding.EnvironmentId);
        Assert.Equal("VAL-RES-PRESENCE", finding.FixCode);
    }

    [Fact]
    public void LinkedTargetMissingFromSourceEnvironmentProducesFinding()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var app = Resource("ContainerApp", "api", Guid.NewGuid());
        var insights = Resource("ApplicationInsights", "appi", Guid.NewGuid());
        var source = Component(Guid.NewGuid(), "orders", "PerEnvironment", [app], [devId, prdId]);
        var target = Component(Guid.NewGuid(), "core", "PerEnvironment", [insights], [devId]) with
        {
            TargetsAllEnvironments = false
        };
        var link = Link(source.Id, app.Id, target.Id, insights.Id, LinkKind.Telemetry);
        var snapshot = Snapshot([source, target], [link], [Environment(devId, "dev"), Environment(prdId, "prd")]);

        var result = CreateEngine([new ComponentEnvironmentRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-CMP-ENVIRONNEMENT", finding.Code);
        Assert.Equal(link.Id, finding.Subject.Id);
        Assert.Equal(prdId, finding.EnvironmentId);
    }

    [Fact]
    public void HostingLinkAcrossRegionsProducesPlacementError()
    {
        var environmentId = Guid.NewGuid();
        var app = Resource("ContainerApp", "api", Guid.NewGuid());
        var hostingEnvironment = Resource("ContainerAppsEnvironment", "main", Guid.NewGuid());
        var source = Component(Guid.NewGuid(), "orders", "PerEnvironment", [app], [environmentId]);
        var target = Component(Guid.NewGuid(), "platform", "PerEnvironment", [hostingEnvironment], [environmentId]) with
        {
            ResourceGroups = Component(Guid.NewGuid(), "temp", "PerEnvironment", [hostingEnvironment], [environmentId])
                .ResourceGroups.Select(group => group with { Region = "uksouth" }).ToArray()
        };
        var link = Link(source.Id, app.Id, target.Id, hostingEnvironment.Id, LinkKind.Hosting);
        var snapshot = Snapshot([source, target], [link], [Environment(environmentId, "dev")]);

        var result = CreateEngine([new LinkPlacementRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-LIA-PLACEMENT", finding.Code);
        Assert.Equal(link.Id, finding.Subject.Id);
        Assert.Equal(environmentId, finding.EnvironmentId);
    }

    [Fact]
    public void UserAssignedIdentityFromAnotherComponentProducesError()
    {
        var app = Resource("ContainerApp", "api", Guid.NewGuid());
        var registry = Resource("ContainerRegistry", "registry", Guid.NewGuid());
        var identity = Resource("UserAssignedIdentity", "identity", Guid.NewGuid());
        var source = Component(Guid.NewGuid(), "orders", "PerEnvironment", [app]);
        var target = Component(Guid.NewGuid(), "platform", "Single", [registry], singleTargetId: Guid.NewGuid());
        var identityComponent = Component(Guid.NewGuid(), "core", "PerEnvironment", [identity]);
        var link = Link(source.Id, app.Id, target.Id, registry.Id, LinkKind.ImagePull,
            LinkIdentityKind.UserAssigned, identity.Id);
        var snapshot = Snapshot([source, target, identityComponent], [link]);

        var result = CreateEngine([new SharedIdentityRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-LIA-IDENTITE-PARTAGEE", finding.Code);
        Assert.Equal(identity.Id, finding.Subject.Id);
        Assert.Contains(link.Id, finding.RelatedLinkIds);
    }

    [Fact]
    public void GeneratedNameOutsideCatalogLengthProducesFixableError()
    {
        var environmentId = Guid.NewGuid();
        var resource = Resource("LogAnalyticsWorkspace", "very-long-log-name", Guid.NewGuid());
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", [resource], [environmentId]);
        var snapshot = Snapshot([component], [], [Environment(environmentId, "dev")]) with
        {
            NamingRules = new Dictionary<string, NamingRuleSnapshot>(StringComparer.OrdinalIgnoreCase)
            {
                ["LogAnalyticsWorkspace"] = NamingRule(10),
                [BuiltInNamingRules.ResourceGroupType] = NamingRule(64)
            }
        };

        var result = CreateEngine([new NamingLengthRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-NOM-LONGUEUR", finding.Code);
        Assert.Equal(resource.Id, finding.Subject.Id);
        Assert.Equal("VAL-NOM-LONGUEUR", finding.FixCode);
    }

    [Fact]
    public void LiteralAppSettingWithSecretLikeNameWarnsWithoutExposingValue()
    {
        const string secretValue = "must-not-appear-in-finding";
        var setting = AppSetting(Guid.NewGuid(), Guid.NewGuid(), "Payments__ApiKey", AppSettingSourceKind.Literal, secretValue);
        var snapshot = Snapshot([], []) with { AppSettings = [setting] };

        var result = CreateEngine([new AppSettingSecretSuspectRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PAR-SECRET-SUSPECT", finding.Code);
        Assert.Null(finding.FixCode);
        Assert.DoesNotContain(secretValue, finding.MessageFr, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, finding.MessageEn, StringComparison.Ordinal);
    }

    [Fact]
    public void LiteralAppSettingDoesNotOfferFixWhenPipelineVaultDeploysAfterApplication()
    {
        var applicationComponentId = Guid.NewGuid();
        var vaultComponentId = Guid.NewGuid();
        var application = Resource("AppService", "api", Guid.NewGuid());
        var vault = Resource("KeyVault", "main", Guid.NewGuid());
        var setting = AppSetting(
            Guid.NewGuid(), applicationComponentId, "Payments__ApiKey", AppSettingSourceKind.Literal,
            "must-not-appear", application.Id);
        var snapshot = Snapshot(
        [
            Component(applicationComponentId, "api", "PerEnvironment", [application]),
            Component(vaultComponentId, "zvault", "PerEnvironment", [vault])
        ], []) with { AppSettings = [setting] };

        var result = CreateEngine([new AppSettingSecretSuspectRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Null(finding.FixCode);
    }

    [Fact]
    public void LiteralAppSettingOffersFixWhenPipelineVaultDeploysBeforeApplication()
    {
        var applicationComponentId = Guid.NewGuid();
        var vaultComponentId = Guid.NewGuid();
        var application = Resource("AppService", "api", Guid.NewGuid());
        var vault = Resource("KeyVault", "main", Guid.NewGuid());
        var setting = AppSetting(
            Guid.NewGuid(), applicationComponentId, "Payments__ApiKey", AppSettingSourceKind.Literal,
            "must-not-appear", application.Id);
        var snapshot = Snapshot(
        [
            Component(applicationComponentId, "api", "PerEnvironment", [application]),
            Component(vaultComponentId, "aaa", "PerEnvironment", [vault])
        ], []) with { AppSettings = [setting] };

        var result = CreateEngine([new AppSettingSecretSuspectRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PAR-SECRET-SUSPECT", finding.FixCode);
    }

    [Fact]
    public void DuplicateAppSettingNameProducesConflictFindings()
    {
        var applicationResourceId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var first = AppSetting(Guid.NewGuid(), componentId, "Payments__ApiKey", AppSettingSourceKind.Literal, "one", applicationResourceId);
        var second = AppSetting(Guid.NewGuid(), componentId, "payments__apikey", AppSettingSourceKind.Literal, "two", applicationResourceId);
        var snapshot = Snapshot([], []) with { AppSettings = [first, second] };

        var result = CreateEngine([new AppSettingConflictRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, finding => Assert.Equal("VAL-PAR-SECRET-CONFLIT", finding.Code));
    }

    [Fact]
    public void KeyVaultSecretWrittenBySeveralComponentsProducesConflict()
    {
        var vaultId = Guid.NewGuid();
        var first = AppSetting(Guid.NewGuid(), Guid.NewGuid(), "Payments__ApiKey", AppSettingSourceKind.KeyVaultSecret, null, keyVaultId: vaultId, secretName: "payments-api-key");
        var second = AppSetting(Guid.NewGuid(), Guid.NewGuid(), "Orders__ApiKey", AppSettingSourceKind.KeyVaultSecret, null, keyVaultId: vaultId, secretName: "payments-api-key");
        var snapshot = Snapshot([], []) with { AppSettings = [first, second] };

        var result = CreateEngine([new AppSettingSecretOwnerRule()]).Evaluate(snapshot, Catalog);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, finding => Assert.Equal("VAL-PAR-SECRET-PROPRIETAIRE", finding.Code));
    }

    [Fact]
    public void SecretManagedOutsideIfsProducesInformationalFinding()
    {
        var setting = AppSetting(Guid.NewGuid(), Guid.NewGuid(), "Payments__ApiKey", AppSettingSourceKind.KeyVaultSecret, null,
            secretFeedKind: AppSettingSecretFeedKind.ManagedOutsideIfs);
        var snapshot = Snapshot([], []) with { AppSettings = [setting] };

        var result = CreateEngine([new AppSettingManagedOutsideIfsRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-PAR-HORS-IFS", finding.Code);
        Assert.Equal(FindingSeverity.Info, finding.Severity);
    }

    [Fact]
    public void ComponentCycleFindingIncludesEveryLinkInTheCycle()
    {
        var firstComponentId = Guid.NewGuid();
        var secondComponentId = Guid.NewGuid();
        var firstWorkspace = Resource("LogAnalyticsWorkspace", "main", Guid.NewGuid());
        var firstInsights = Resource("ApplicationInsights", "main", firstWorkspace.ResourceGroupId);
        var secondWorkspace = Resource("LogAnalyticsWorkspace", "main", Guid.NewGuid());
        var secondInsights = Resource("ApplicationInsights", "main", secondWorkspace.ResourceGroupId);
        var firstLink = Link(firstComponentId, firstInsights.Id, secondComponentId, secondWorkspace.Id, LinkKind.Logging);
        var secondLink = Link(secondComponentId, secondInsights.Id, firstComponentId, firstWorkspace.Id, LinkKind.Logging);
        var snapshot = Snapshot(
        [
            Component(firstComponentId, "first", "PerEnvironment", [firstWorkspace, firstInsights]),
            Component(secondComponentId, "second", "PerEnvironment", [secondWorkspace, secondInsights])
        ],
        [firstLink, secondLink]);

        var result = CreateEngine([new ComponentCycleRule()]).Evaluate(snapshot, Catalog);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("VAL-CMP-CYCLE", finding.Code);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal(new[] { firstLink.Id, secondLink.Id }.Order(), finding.RelatedLinkIds.Order());
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ValidatesThreeHundredResourcesInUnderTwoSeconds()
    {
        var groupId = Guid.NewGuid();
        var resources = Enumerable.Range(0, 300)
            .Select(index => Resource("LogAnalyticsWorkspace", $"workspace-{index:D3}", groupId))
            .ToArray();
        var component = Component(Guid.NewGuid(), "core", "PerEnvironment", resources);
        var snapshot = Snapshot([component], []);
        var stopwatch = Stopwatch.StartNew();

        _ = CreateEngine().Evaluate(snapshot, Catalog);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Validation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private static ValidationEngine CreateEngine(IReadOnlyList<IValidationRule>? rules = null)
    {
        var registry = new LinkKindRegistry(PilotLinkKindHandlers.Create());
        var wiring = new WiringEngine(registry, new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]));
        return new ValidationEngine(
            [new CodeRuleSource(rules ?? PilotValidationRules.Create())],
            new ComponentOrdering(wiring),
            registry,
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
    }

    private static ValidationContext Context(ModelSnapshot snapshot, WiringResult wiring)
    {
        var linkKinds = new LinkKindRegistry(PilotLinkKindHandlers.Create());
        return new ValidationContext(
            snapshot,
            Catalog,
            new ComponentOrderingResult([], [], wiring),
            NamingEngine.Preview(snapshot, linkKinds),
            linkKinds,
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
    }

    private static CatalogPropertyValueFailure? InvokeConstraintFailure(JsonElement value, JsonElement definition) =>
        CatalogPropertyValueValidator.Validate(value, definition);

    private static ApplicationSnapshot CreateApplication(Guid componentId, Guid resourceId, string codeSourcePath) => new(
        Guid.NewGuid(),
        componentId,
        resourceId,
        "api",
        codeSourcePath,
        "src/api/Dockerfile",
        "src/api",
        "shop/orders/api",
        null,
        DeploymentStrategyKind.Direct,
        null,
        []);

    private static ReferenceFixture ReferenceProject()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var coreId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var ordersId = Guid.NewGuid();
        var coreGroupId = Guid.NewGuid();
        var dataGroupId = Guid.NewGuid();
        var platformGroupId = Guid.NewGuid();
        var ordersGroupId = Guid.NewGuid();
        var workspace = Resource("LogAnalyticsWorkspace", "log main", coreGroupId);
        var insights = Resource("ApplicationInsights", "appi main", coreGroupId);
        var keyVault = Resource("KeyVault", "kv main", coreGroupId);
        var sqlServer = Resource("SqlServer", "sql orders", dataGroupId);
        var sqlDatabase = Resource("SqlDatabase", "sqldb orders", dataGroupId);
        var registry = Resource("ContainerRegistry", "cr main", platformGroupId);
        var identity = Resource("UserAssignedIdentity", "id api", ordersGroupId);
        var environment = Resource("ContainerAppsEnvironment", "cae main", ordersGroupId);
        var app = Resource("ContainerApp", "ca api", ordersGroupId);
        var components = new[]
        {
            Component(coreId, "core", "PerEnvironment", [workspace, insights, keyVault], [devId, prdId]),
            Component(dataId, "data", "PerEnvironment", [sqlServer, sqlDatabase], [devId, prdId], workspace.Id),
            Component(platformId, "platform", "Single", [registry], [], singleTargetId: sharedId),
            Component(ordersId, "orders", "PerEnvironment", [identity, environment, app], [devId, prdId], workspace.Id)
        };
        var links = new[]
        {
            Link(coreId, insights.Id, coreId, workspace.Id, LinkKind.Logging),
            Link(dataId, sqlDatabase.Id, dataId, sqlServer.Id, LinkKind.Hosting),
            Link(ordersId, environment.Id, coreId, workspace.Id, LinkKind.Logging),
            Link(ordersId, app.Id, ordersId, environment.Id, LinkKind.Hosting),
            Link(ordersId, app.Id, platformId, registry.Id, LinkKind.ImagePull, LinkIdentityKind.UserAssigned, identity.Id),
            Link(ordersId, app.Id, coreId, insights.Id, LinkKind.Telemetry),
            Link(ordersId, app.Id, coreId, workspace.Id, LinkKind.Access, LinkIdentityKind.UserAssigned, identity.Id, ["Log Analytics Reader"]),
            Link(ordersId, app.Id, dataId, sqlDatabase.Id, LinkKind.DataAccess, LinkIdentityKind.System, null, dataAccessLevel: DataAccessLevel.LectureEcriture)
        };
        var snapshot = Snapshot(components, links, [
            new EnvironmentSnapshot("dev", "francecentral") { Id = devId, Position = 1, SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
            new EnvironmentSnapshot("prd", "francecentral") { Id = prdId, Position = 2, SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), IsProtected = true, Approvers = ["Shop Release Approvers"] }
        ]);
        return new ReferenceFixture(snapshot, app.Id);
    }

    private static ModelSnapshot Snapshot(
        IReadOnlyList<ComponentSnapshot> components,
        IReadOnlyList<LinkSnapshot> links,
        IReadOnlyList<EnvironmentSnapshot>? environments = null) => new(
        new ProjectSnapshot("Boutique en ligne", "shop", "")
        {
            Id = Guid.NewGuid(),
            CatalogVersion = Catalog.Version
        },
        environments ?? [],
        new NamingConventionSnapshot("Compact", "{abbr}-{project}-{name}-{env}", "rg-{project}-{component}-{name}-{env}"),
        new Dictionary<string, string>(),
        components)
    {
        Links = links
    };

    private static ResourceSnapshot FindResource(ModelSnapshot snapshot, Guid resourceId) =>
        snapshot.Components.SelectMany(component => component.Resources)
            .Single(resource => resource.Id == resourceId);

    private static ModelSnapshot ReplaceResource(ModelSnapshot snapshot, ResourceSnapshot replacement) => snapshot with
    {
        Components = snapshot.Components.Select(component => component with
        {
            Resources = component.Resources.Select(resource => resource.Id == replacement.Id ? replacement : resource).ToArray()
        }).ToArray()
    };

    private static EnvironmentSnapshot Environment(Guid id, string code) => new(code, "francecentral")
    {
        Id = id,
        Name = code.ToUpperInvariant(),
        Position = 1,
        SubscriptionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    };

    private static NamingRuleSnapshot NamingRule(int maximumLength) => new(
        "log",
        "^[a-z0-9-]+$",
        "lower",
        ["-"],
        true,
        2,
        maximumLength,
        "letter",
        "alphanumeric",
        "resourceGroup",
        null);

    private static AppSettingSnapshot AppSetting(
        Guid id,
        Guid componentId,
        string name,
        AppSettingSourceKind sourceKind,
        string? literalValue,
        Guid? applicationResourceId = null,
        Guid? keyVaultId = null,
        string? secretName = null,
        AppSettingSecretFeedKind? secretFeedKind = null) => new(
        id,
        componentId,
        applicationResourceId ?? Guid.NewGuid(),
        name,
        sourceKind,
        literalValue,
        new Dictionary<Guid, string>(),
        null,
        null,
        keyVaultId,
        secretName,
        secretFeedKind,
        null,
        null);

    private static ComponentSnapshot Component(
        Guid id,
        string code,
        string mode,
        IReadOnlyList<ResourceSnapshot> resources,
        IReadOnlyList<Guid>? targets = null,
        Guid? defaultLogAnalyticsResourceId = null,
        Guid? singleTargetId = null)
    {
        var resourceGroups = resources.Select(resource => resource.ResourceGroupId).Distinct()
            .Select(resourceGroupId => new ResourceGroupSnapshot(resourceGroupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>()))
            .ToArray();
        return new ComponentSnapshot(
            id,
            code,
            code,
            mode,
            targets ?? [],
            singleTargetId is { } targetId
                ? new ComponentSingleTargetSnapshot(targetId, "shared", Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "francecentral", "ifs-shop-shared", true, ["Shop Release Approvers"], [])
                : null,
            resourceGroups,
            resources)
        {
            DefaultLogAnalyticsResourceId = defaultLogAnalyticsResourceId,
            TargetsAllEnvironments = mode == "PerEnvironment"
        };
    }

    private static ResourceSnapshot Resource(string type, string logicalName, Guid groupId) => new(
        Guid.NewGuid(),
        groupId,
        type,
        logicalName,
        string.Empty,
        false,
        new Dictionary<string, JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);

    private static LinkSnapshot Link(
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind = LinkIdentityKind.None,
        Guid? identityResourceId = null,
        IReadOnlyList<string>? roles = null,
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

    private sealed record ReferenceFixture(ModelSnapshot Snapshot, Guid ContainerAppId);

    private sealed class TestRule(
        string code,
        string lot,
        FindingSeverity severity = FindingSeverity.Info) : IValidationRule
    {
        public string Code => code;

        public FindingSeverity Severity => severity;

        public string Lot => lot;

        public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) =>
        [
            context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Project, context.Snapshot.Project.Id),
                Code,
                Code)
        ];
    }
}
