using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
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
        using var definition = JsonDocument.Parse("{\"pattern\":\"(\"}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Contains("motif", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogRegexValidationDoesNotThrowWhenPatternExceedsItsTimeLimit()
    {
        using var value = JsonDocument.Parse($"\"{new string('a', 5000)}!\"");
        using var definition = JsonDocument.Parse("{\"pattern\":\"(a+)+$\"}");

        var failure = InvokeConstraintFailure(value.RootElement, definition.RootElement);

        Assert.Contains("motif", failure, StringComparison.OrdinalIgnoreCase);
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
            NamingEngine.Preview(snapshot),
            linkKinds,
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
    }

    private static string? InvokeConstraintFailure(JsonElement value, JsonElement definition) =>
        (string?)typeof(ResourceConstraintRule)
            .GetMethod("GetConstraintFailure", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [value, definition]);

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
