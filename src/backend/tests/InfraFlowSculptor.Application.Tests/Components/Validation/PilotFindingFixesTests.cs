using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;

namespace InfraFlowSculptor.Application.Tests.Components.Validation;

public sealed class PilotFindingFixesTests
{
    [Fact]
    public void RestrictedExposureFindingDoesNotOfferAnAutomaticFix()
    {
        var registry = new FindingFixRegistry(
            PilotFindingFixes.Create(new LinkKindRegistry(PilotLinkKindHandlers.Create())));

        Assert.False(registry.Supports("VAL-NET-IP-VIDE"));
    }

    [Fact]
    public void RequiredLinkFixCannotAddASecondHostingLinkBesideAnInvalidExistingOne()
    {
        var projectId = Guid.NewGuid();
        var databaseComponentId = Guid.NewGuid();
        var serverComponentId = Guid.NewGuid();
        var databaseGroupId = Guid.NewGuid();
        var serverGroupId = Guid.NewGuid();
        var database = CreateResource(databaseGroupId, "SqlDatabase", "orders");
        var localServer = CreateResource(databaseGroupId, "SqlServer", "local-server");
        var externalServer = CreateResource(serverGroupId, "SqlServer", "external-server");
        var databaseComponent = CreateComponent(databaseComponentId, "data", [database, localServer]);
        var serverComponent = CreateComponent(serverComponentId, "platform", [externalServer]);
        var invalidLink = new LinkSnapshot(
            Guid.NewGuid(), databaseComponentId, database.Id, serverComponentId, externalServer.Id,
            LinkKind.Hosting, LinkOrigin.Explicit, LinkIdentityKind.None, null, [], LinkAccessScope.Resource, null);
        var catalog = CatalogLoader.Load().LatestPublished!;
        var snapshot = new ModelSnapshot(
            new ProjectSnapshot("Project", "project", string.Empty) { Id = projectId, CatalogVersion = catalog.Version },
            [],
            new NamingConventionSnapshot("Compact", "{name}", "rg-{name}"),
            new Dictionary<string, string>(),
            [databaseComponent, serverComponent])
        {
            Links = [invalidLink]
        };
        var wiringIssue = new WiringIssue("VAL-LIA-OBLIGATOIRE", "Missing required link.", invalidLink.Id, database.Id)
        {
            Kind = LinkKind.Hosting
        };
        var ordering = new ComponentOrderingResult([], [], new WiringResult([], [], [], [], [], [], [wiringIssue]));
        var finding = new ValidationFinding(
            "VAL-LIA-OBLIGATOIRE",
            FindingSeverity.Error,
            "lot1",
            new ObjectRef(ObjectType.Resource, database.Id),
            null,
            "La liaison d'hébergement obligatoire manque.",
            "The required hosting link is missing.",
            null,
            "VAL-LIA-OBLIGATOIRE",
            [],
            LinkKind.Hosting.ToString());
        var validation = new ValidationResult(
            projectId,
            0,
            catalog.Version,
            [],
            [finding],
            new ValidationReadiness(false, false, false, false),
            ordering);
        var context = new FindingFixContext(projectId, snapshot, catalog, validation, finding);
        var fix = PilotFindingFixes.Create(new LinkKindRegistry(PilotLinkKindHandlers.Create()))
            .Single(value => value.Key == "VAL-LIA-OBLIGATOIRE");

        var result = fix.CreateCommand(context);

        Assert.True(result.IsError);
        Assert.Equal("VALIDATION_FIX_LINK_MUST_BE_REPAIRED", result.FirstError.Code);
    }

    private static ComponentSnapshot CreateComponent(Guid id, string code, IReadOnlyList<ResourceSnapshot> resources) => new(
        id,
        code,
        code,
        "PerEnvironment",
        [],
        null,
        resources.Select(resource => resource.ResourceGroupId).Distinct()
            .Select(groupId => new ResourceGroupSnapshot(groupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>()))
            .ToArray(),
        resources);

    private static ResourceSnapshot CreateResource(Guid groupId, string type, string name) => new(
        Guid.NewGuid(),
        groupId,
        type,
        name,
        string.Empty,
        false,
        new Dictionary<string, JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);
}
