using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Tests.Links;

public sealed class WiringEngineTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot")!;
    private static readonly CatalogVersion PatchedCatalog = CatalogLoader.Load().GetVersion("2026.10-pilot.1")!;
    private static readonly string[] SqlReadWriteRoles = ["db_datareader", "db_datawriter"];

    [Fact]
    public void DerivesDefaultDiagnosticsAsImplicitCrossComponentLink()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var workspaceResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "data", Resource(sourceResourceId, "ContainerApp")) with
        {
            DefaultLogAnalyticsResourceId = workspaceResourceId
        };
        var target = Component(targetComponentId, "core", Resource(workspaceResourceId, "LogAnalyticsWorkspace"));

        var result = CreateEngine().Evaluate(Snapshot(source, target), Catalog);

        var link = Assert.Single(result.Links);
        Assert.Equal(LinkKind.Diagnostics, link.Link.Kind);
        Assert.Equal(LinkOrigin.Implicit, link.Link.Origin);
        Assert.True(link.IsExternal);
        Assert.Single(result.Dependencies);
    }

    [Fact]
    public void DefaultDiagnosticsDoesNotLinkWorkspaceToItself()
    {
        var componentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var workspaceResourceId = Guid.NewGuid();
        var component = Component(componentId, "core",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(workspaceResourceId, "LogAnalyticsWorkspace")) with
        {
            DefaultLogAnalyticsResourceId = workspaceResourceId
        };

        var result = CreateEngine().Evaluate(Snapshot(component), Catalog);

        var link = Assert.Single(result.Links);
        Assert.Equal(sourceResourceId, link.Link.SourceResourceId);
        Assert.Equal(workspaceResourceId, link.Link.TargetResourceId);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "RG-LIA-02");
    }

    [Fact]
    public void DefaultDiagnosticsIgnoresExistingResourcesAsSources()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var workspaceResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "orders", Resource(sourceResourceId, "ContainerApp", isExisting: true)) with
        {
            DefaultLogAnalyticsResourceId = workspaceResourceId
        };
        var target = Component(targetComponentId, "core", Resource(workspaceResourceId, "LogAnalyticsWorkspace"));

        var result = CreateEngine().Evaluate(Snapshot(source, target), Catalog);

        Assert.DoesNotContain(result.Links, link => link.Link.SourceResourceId == sourceResourceId);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "VAL-LIA-SOURCE-EXISTING");
    }

    [Fact]
    public void DeduplicatesRoleFromMultipleOriginsAndKeepsItWhenOneOriginIsRemoved()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "ContainerRegistry"));
        var imagePull = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.ImagePull, LinkIdentityKind.UserAssigned, identityResourceId);
        var access = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["AcrPull"]);

        var full = CreateEngine().Evaluate(Snapshot(source, target, imagePull, access), Catalog);
        var assignment = Assert.Single(full.RoleAssignments);
        Assert.Equal("AcrPull", assignment.RoleName);
        Assert.Equal(sourceComponentId, assignment.OwnerComponentId);
        Assert.Equal(2, assignment.Origins.Count);

        var remaining = CreateEngine().Evaluate(Snapshot(source, target, imagePull), Catalog);
        Assert.Single(remaining.RoleAssignments);
    }

    [Fact]
    public void RoleAssignmentNameIsStableAcrossEvaluations()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "ContainerRegistry"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.ImagePull, LinkIdentityKind.UserAssigned, identityResourceId);
        var snapshot = Snapshot(source, target, link);

        var first = Assert.Single(CreateEngine().Evaluate(snapshot, Catalog).RoleAssignments);
        var second = Assert.Single(CreateEngine().Evaluate(snapshot, Catalog).RoleAssignments);

        Assert.Equal(first.DeterministicName, second.DeterministicName);
    }

    [Fact]
    public void SecretReadIsSatisfiedByEquivalentExplicitKeyVaultRole()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "KeyVault"));
        var explicitAccess = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Key Vault Secrets Officer"]);
        var implicitRead = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.SecretRead, LinkIdentityKind.UserAssigned, identityResourceId) with { Origin = LinkOrigin.Implicit };

        var result = CreateEngine().Evaluate(Snapshot(source, target, explicitAccess, implicitRead), Catalog);

        var assignment = Assert.Single(result.RoleAssignments);
        Assert.Equal("Key Vault Secrets Officer", assignment.RoleName);
        var equivalence = Assert.Single(result.RoleEquivalences);
        Assert.Equal("Key Vault Secrets User", equivalence.RequestedRole);
        Assert.Equal("Key Vault Secrets Officer", equivalence.SatisfiedByRole);
    }

    [Fact]
    public void AzureSqlDataAccessUsesConsumerComponentAndIdempotentScripts()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "data", Resource(targetResourceId, "SqlDatabase"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.DataAccess, LinkIdentityKind.System, null) with { DataAccessLevel = DataAccessLevel.LectureEcriture };

        var request = Assert.Single(CreateEngine().Evaluate(Snapshot(source, target, link), Catalog).DataAccessRequests);

        Assert.Equal(sourceComponentId, request.OwnerComponentId);
        Assert.Equal(SqlReadWriteRoles, request.DatabaseRoles);
        Assert.Contains("ifs-managed", request.ApplyScript, StringComparison.Ordinal);
        Assert.Contains("DROP USER", request.RemoveScript, StringComparison.Ordinal);
    }

    [Fact]
    public void DataAccessLevelOnAccessLinkDoesNotCreateDatabaseRequest()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "vault", Resource(targetResourceId, "KeyVault"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.System, null, ["Key Vault Secrets User"]) with
        {
            DataAccessLevel = DataAccessLevel.Schema
        };

        var result = CreateEngine().Evaluate(Snapshot(source, target, link), Catalog);

        Assert.Empty(result.DataAccessRequests);
        Assert.Empty(result.Links);
        Assert.Empty(result.Dependencies);
        Assert.Empty(result.RoleAssignments);
        Assert.Contains(result.Issues, issue => issue.Code == "VAL-LIA-DATA-LEVEL-UNSUPPORTED");
    }

    [Fact]
    public void UnsupportedDataAccessProviderDoesNotProducePartialWiringEffects()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "data", Resource(targetResourceId, "SqlDatabase"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.DataAccess, LinkIdentityKind.System, null) with { DataAccessLevel = DataAccessLevel.LectureEcriture };
        var engine = new WiringEngine(
            new LinkKindRegistry(PilotLinkKindHandlers.Create()),
            new DataAccessProviderRegistry([]));

        var result = engine.Evaluate(Snapshot(source, target, link), Catalog);

        Assert.Contains(result.Issues, issue => issue.Code == "VAL-LIA-DATA-ENGINE");
        Assert.Empty(result.Links);
        Assert.Empty(result.Identities);
        Assert.Empty(result.RoleAssignments);
        Assert.Empty(result.Dependencies);
        Assert.Empty(result.DataAccessRequests);
    }

    [Fact]
    public void InvalidOptionalIdentityDoesNotProduceWiredLinkOrDependency()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var missingIdentityResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "monitoring", Resource(
            targetResourceId,
            "ApplicationInsights",
            properties: new Dictionary<string, JsonElement>
            {
                ["disableLocalAuth"] = JsonDocument.Parse("false").RootElement.Clone()
            }));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Telemetry, LinkIdentityKind.UserAssigned, missingIdentityResourceId);

        var result = CreateEngine().Evaluate(Snapshot(source, target, link), Catalog);

        Assert.Empty(result.Links);
        Assert.Empty(result.Dependencies);
        Assert.Contains(result.Issues, issue => issue.Code == "VAL-LIA-IDENTITE");
    }

    [Fact]
    public void ForbiddenRoleDetectionIgnoresCase()
    {
        Assert.True(RoleAssignmentPolicy.IsForbiddenRole("owner"));
        Assert.True(RoleAssignmentPolicy.IsForbiddenRole(" USER ACCESS ADMINISTRATOR "));
    }

    [Fact]
    public void RejectsRoleNotDeclaredForTarget()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "ContainerRegistry"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Owner"]);

        var result = CreateEngine().Evaluate(Snapshot(source, target, link), Catalog);

        Assert.Empty(result.RoleAssignments);
        Assert.Contains(result.Issues, issue => issue.Code == "VAL-LIA-ROLE");
    }

    [Fact]
    public void ReportsRequiredLinkWhenCatalogDeclaresOne()
    {
        var componentId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var component = Component(componentId, "monitoring", Resource(resourceId, "ApplicationInsights"));

        var result = CreateEngine().Evaluate(Snapshot(component), Catalog);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "VAL-LIA-OBLIGATOIRE" && issue.ResourceId == resourceId);
    }

    [Fact]
    public void TelemetryRequiresIdentityAndMetricsRoleWhenLocalAuthenticationIsDisabled()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "monitoring", Resource(
            targetResourceId,
            "ApplicationInsights",
            properties: new Dictionary<string, JsonElement>
            {
                ["disableLocalAuth"] = JsonDocument.Parse("true").RootElement.Clone()
            }));
        var withoutIdentity = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Telemetry, LinkIdentityKind.None, null);

        var missingIdentity = CreateEngine().Evaluate(Snapshot(source, target, withoutIdentity), Catalog);
        Assert.Contains(missingIdentity.Issues, issue => issue.Code == "RG-LIA-08-IDENTITY-REQUIRED");

        var withIdentity = withoutIdentity with { IdentityKind = LinkIdentityKind.System };
        var wired = CreateEngine().Evaluate(Snapshot(source, target, withIdentity), Catalog);
        var role = Assert.Single(wired.RoleAssignments);
        Assert.Equal("Monitoring Metrics Publisher", role.RoleName);
    }

    [Fact]
    public void TelemetryWithoutIdentityIsWiredWhenLocalAuthenticationIsEnabled()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "monitoring", Resource(
            targetResourceId,
            "ApplicationInsights",
            properties: new Dictionary<string, JsonElement>
            {
                ["disableLocalAuth"] = JsonDocument.Parse("false").RootElement.Clone()
            }));
        var telemetry = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Telemetry, LinkIdentityKind.None, null);

        var result = CreateEngine().Evaluate(Snapshot(source, target, telemetry), Catalog);

        Assert.Contains(result.Links, link => link.Link.Id == telemetry.Id);
        Assert.Contains(result.Dependencies, dependency => dependency.LinkId == telemetry.Id);
        Assert.Empty(result.Identities);
        Assert.Empty(result.RoleAssignments);
        Assert.DoesNotContain(result.Issues, issue => issue.LinkId == telemetry.Id);
    }

    [Fact]
    public void LinkRemovalImpactWarnsAboutRoleRevocation()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "KeyVault"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Key Vault Secrets User"]);
        var snapshot = Snapshot(source, target, link);
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeLinkRemoval(snapshot, link.Id, Catalog);

        Assert.Equal(ImpactSeverity.Critical, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-ROLE-REVOKED"
            && item.Message == "Accès révoqué dans Azure au prochain déploiement, y compris en production.");
    }

    [Fact]
    public void ResourceDeletionImpactListsImplicitDiagnosticsThatDisappear()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var workspaceResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "orders", Resource(sourceResourceId, "ContainerApp")) with
        {
            DefaultLogAnalyticsResourceId = workspaceResourceId
        };
        var target = Component(targetComponentId, "core", Resource(workspaceResourceId, "LogAnalyticsWorkspace"));
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeResourceDeletion(Snapshot(source, target), workspaceResourceId, Catalog);

        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-LINK-IMPLICITE-RETIRE"
            && item.Severity == ImpactSeverity.Information
            && item.ResourceId == sourceResourceId);
    }

    [Fact]
    public void ResourceDeletionImpactBlocksDeletingIdentityUsedByLink()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "orders",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "KeyVault"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Key Vault Secrets User"]);
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeResourceDeletion(Snapshot(source, target, link), identityResourceId, Catalog);

        Assert.Equal(ImpactSeverity.Blocker, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-IDENTITE-UTILISEE"
            && item.Severity == ImpactSeverity.Blocker
            && item.LinkId == link.Id);
    }

    [Fact]
    public void LinkRemovalImpactBlocksRemovalOfRequiredLoggingLink()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "monitoring", Resource(sourceResourceId, "ApplicationInsights"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "LogAnalyticsWorkspace"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Logging, LinkIdentityKind.None, null);
        var snapshot = Snapshot(source, target, link);
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeLinkRemoval(snapshot, link.Id, Catalog);

        Assert.Equal(ImpactSeverity.Blocker, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-OBLIGATOIRE-ROMPU");
    }

    [Fact]
    public void LinkUpdateImpactAllowsRetargetingRequiredLoggingLink()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var replacementResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "monitoring", Resource(sourceResourceId, "ApplicationInsights"));
        var target = Component(targetComponentId, "core",
            Resource(targetResourceId, "LogAnalyticsWorkspace"),
            Resource(replacementResourceId, "LogAnalyticsWorkspace"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Logging, LinkIdentityKind.None, null);
        var snapshot = Snapshot(source, target, link);
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeLinkUpdate(snapshot, link with { TargetResourceId = replacementResourceId }, Catalog);

        Assert.Equal(ImpactSeverity.Information, impact.Severity);
        Assert.Empty(impact.Items);
    }

    [Fact]
    public void LinkUpdateImpactWarnsAboutRoleRevocation()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "KeyVault"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Key Vault Secrets User"]);
        var snapshot = Snapshot(source, target, link);
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeLinkUpdate(
            snapshot,
            link with { Roles = ["Key Vault Secrets Officer"] },
            Catalog);

        Assert.Equal(ImpactSeverity.Critical, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-ROLE-REVOKED");
    }

    [Fact]
    public void LinkRemovalImpactWarnsAboutDatabaseRoleRevocation()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "app", Resource(sourceResourceId, "ContainerApp"));
        var target = Component(targetComponentId, "data", Resource(targetResourceId, "SqlDatabase"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.DataAccess, LinkIdentityKind.System, null) with
        {
            DataAccessLevel = DataAccessLevel.LectureEcriture
        };
        var analyzer = new ImpactAnalyzer(CreateEngine());

        var impact = analyzer.AnalyzeLinkRemoval(Snapshot(source, target, link), link.Id, Catalog);

        Assert.Equal(ImpactSeverity.Critical, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-DATA-ACCESS-REVOKED"
            && item.ResourceId == targetResourceId);
    }

    [Fact]
    public void AccessToLogAnalyticsCreatesRoleAndRemovalIsCritical()
    {
        var sourceComponentId = Guid.NewGuid();
        var targetComponentId = Guid.NewGuid();
        var sourceResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var hostingResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var source = Component(sourceComponentId, "orders",
            Resource(sourceResourceId, "ContainerApp"),
            Resource(identityResourceId, "UserAssignedIdentity"),
            Resource(hostingResourceId, "ContainerAppsEnvironment"));
        var target = Component(targetComponentId, "core", Resource(targetResourceId, "LogAnalyticsWorkspace"));
        var link = Link(sourceComponentId, sourceResourceId, targetComponentId, targetResourceId,
            LinkKind.Access, LinkIdentityKind.UserAssigned, identityResourceId, ["Log Analytics Reader"]);
        var hosting = Link(sourceComponentId, sourceResourceId, sourceComponentId, hostingResourceId,
            LinkKind.Hosting, LinkIdentityKind.None, null);
        var snapshot = Snapshot(source, target, link, hosting);

        var wiring = CreateEngine().Evaluate(snapshot, PatchedCatalog);
        var assignment = Assert.Single(wiring.RoleAssignments);
        var impact = new ImpactAnalyzer(CreateEngine()).AnalyzeLinkRemoval(
            snapshot, link.Id, PatchedCatalog);

        Assert.Empty(wiring.Issues);
        Assert.Equal("Log Analytics Reader", assignment.RoleName);
        Assert.Equal(ImpactSeverity.Critical, impact.Severity);
        Assert.Contains(impact.Items, item => item.Code == "IMPACT-LIA-ROLE-REVOKED"
            && item.ResourceId == targetResourceId);
    }

    private static WiringEngine CreateEngine() => new(
        new LinkKindRegistry(PilotLinkKindHandlers.Create()),
        new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]));

    private static ModelSnapshot Snapshot(
        params object[] values)
    {
        var components = values.OfType<ComponentSnapshot>().ToArray();
        var links = values.OfType<LinkSnapshot>().ToArray();
        return new ModelSnapshot(
            new ProjectSnapshot("Project", "project", string.Empty),
            [],
            new NamingConventionSnapshot("Compact", "{name}", "rg-{name}"),
            new Dictionary<string, string>(),
            components)
        {
            Links = links
        };
    }

    private static ComponentSnapshot Component(Guid id, string code, params ResourceSnapshot[] resources) => new(
        id,
        code,
        code,
        "PerEnvironment",
        [],
        null,
        resources.Select(resource => resource.ResourceGroupId).Distinct()
            .Select(groupId => new ResourceGroupSnapshot(groupId, "group", null, new Dictionary<string, string>(), new Dictionary<Guid, string>()))
            .ToArray(),
        resources);

    private static ResourceSnapshot Resource(
        Guid id,
        string type,
        bool isExisting = false,
        IReadOnlyDictionary<string, JsonElement>? properties = null)
    {
        var groupId = Guid.NewGuid();
        return new ResourceSnapshot(
            id,
            groupId,
            type,
            $"{type.ToLowerInvariant()}-{id.ToString("N")[..8]}",
            string.Empty,
            isExisting,
            properties ?? new Dictionary<string, JsonElement>(),
            new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
            new Dictionary<Guid, bool>(),
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, string>(),
            new Dictionary<string, string>(),
            []);
    }

    private static LinkSnapshot Link(
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind,
        Guid? identityResourceId,
        IReadOnlyList<string>? roles = null) => new(
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
        null);
}
