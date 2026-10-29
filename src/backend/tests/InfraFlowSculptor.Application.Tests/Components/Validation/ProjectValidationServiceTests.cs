using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Application.Tests.Components.Validation;

public sealed class ProjectValidationServiceTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot.3")!;

    [Fact]
    public async Task CacheKeyIncludesOrganizationNamingReservations()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        using var provider = services.BuildServiceProvider();
        var linkKinds = new LinkKindRegistry(PilotLinkKindHandlers.Create());
        var engine = new ValidationEngine(
            [new OrganizationReservationCountRule()],
            new ComponentOrdering(new WiringEngine(linkKinds, new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]))),
            linkKinds,
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
        var validationService = new ProjectValidationService(provider.GetRequiredService<HybridCache>(), engine);
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var firstSnapshot = Snapshot(projectId, []);
        var secondSnapshot = Snapshot(projectId, [new GlobalNameReservationSnapshot("ContainerRegistry", "crshared", otherProjectId)]);

        var first = await validationService.GetAsync(firstSnapshot, Catalog);
        var second = await validationService.GetAsync(secondSnapshot, Catalog);

        Assert.Equal("reservations:0", Assert.Single(first.Findings).MessageFr);
        Assert.Equal("reservations:1", Assert.Single(second.Findings).MessageFr);
    }

    private static ModelSnapshot Snapshot(Guid projectId, IReadOnlyList<GlobalNameReservationSnapshot> reservations) => new(
        new ProjectSnapshot("Projet", "project", string.Empty)
        {
            Id = projectId,
            ModelVersion = 5,
            CatalogVersion = Catalog.Version
        },
        [],
        new NamingConventionSnapshot("Compact", "{name}", "rg-{name}"),
        new Dictionary<string, string>(),
        [])
    {
        OrganizationGlobalNames = reservations
    };

    private sealed class OrganizationReservationCountRule : IRuleSource
    {
        public string Key => "organization-reservation-test";

        public IReadOnlyList<IValidationRule> GetRules() => [new Rule()];
    }

    private sealed class Rule : IValidationRule
    {
        public string Code => "VAL-TEST-RESERVATION";

        public FindingSeverity Severity => FindingSeverity.Info;

        public string Lot => "lot1";

        public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) =>
        [
            context.Finding(
                Code,
                Severity,
                new InfraFlowSculptor.Domain.Common.ObjectRef(
                    InfraFlowSculptor.Domain.Common.ObjectType.Project,
                    context.Snapshot.Project.Id),
                $"reservations:{context.Snapshot.OrganizationGlobalNames.Count}",
                $"reservations:{context.Snapshot.OrganizationGlobalNames.Count}")
        ];
    }
}
