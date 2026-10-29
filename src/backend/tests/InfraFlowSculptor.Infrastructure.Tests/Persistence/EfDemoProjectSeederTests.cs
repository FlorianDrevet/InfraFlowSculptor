using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Ordering;
using InfraFlowSculptor.Engine.Validation;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class EfDemoProjectSeederTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task SeedShopCreatesTheReferenceModelAndIsIdempotent()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var ownerId = new UserProfileId(Guid.NewGuid());
        var projectId = new ProjectId(Guid.NewGuid());
        var tenantId = Guid.NewGuid();
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(organizationId));
        await dbContext.Database.MigrateAsync();

        var owner = UserProfile.Create(
            ownerId,
            new UserKey(tenantId, Guid.NewGuid()),
            "Alice Martin",
            "alice@contoso.example",
            DateTimeOffset.UtcNow).Value;
        dbContext.UserProfiles.Add(owner);
        dbContext.Organizations.Add(OrganizationAggregate.Create(organizationId, "Contoso", ownerId).Value);
        await dbContext.SaveChangesAsync();

        var seeder = new EfDemoProjectSeeder(dbContext);
        await seeder.SeedShopAsync(
            projectId, organizationId, ownerId, ownerId, ownerId, "2026.10-pilot.3", seedReferenceModel: true);
        await seeder.SeedShopAsync(
            projectId, organizationId, ownerId, ownerId, ownerId, "2026.10-pilot.3", seedReferenceModel: true);

        var project = await dbContext.Projects.IgnoreQueryFilters().SingleAsync(value => value.Id == projectId);
        Assert.Equal("Boutique en ligne", project.Name);
        Assert.Equal("shop", project.Code);
        Assert.Equal("2026.10-pilot.3", project.CatalogVersion);
        Assert.Equal(2, project.Environments.Count);

        var components = await dbContext.Components
            .IgnoreQueryFilters()
            .Where(component => component.ProjectId == projectId)
            .ToArrayAsync();

        Assert.Equal(["core", "data", "orders", "platform"], components.Select(component => component.Code).Order());
        Assert.Equal(8, await dbContext.Set<LinkAggregate>().IgnoreQueryFilters()
            .CountAsync(link => link.ProjectId == projectId));
        Assert.Equal(1, await dbContext.Applications.IgnoreQueryFilters()
            .CountAsync(application => application.ProjectId == projectId));

        var catalogProvider = CatalogLoader.Load();
        var catalog = catalogProvider.GetVersion("2026.10-pilot.3");
        Assert.NotNull(catalog);
        var snapshot = await new ModelSnapshotBuilder(dbContext, catalogProvider).BuildAsync(projectId.Value);
        Assert.False(snapshot.IsError);

        var linkKinds = new LinkKindRegistry(PilotLinkKindHandlers.Create());
        var engine = new ValidationEngine(
            [new CodeRuleSource(PilotValidationRules.Create())],
            new ComponentOrdering(new WiringEngine(
                linkKinds,
                new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()]))),
            linkKinds,
            new DeploymentStrategyRegistry([new DirectDeploymentStrategy()]));
        var findings = engine.Evaluate(snapshot.Value, catalog!).Findings;
        Assert.Equal(
            ["VAL-LIA-SQL-ADMIN", "VAL-LIA-PORTEE-EXTERNE"],
            findings.Select(finding => finding.Code));
        Assert.Equal(1, findings.Count(finding => finding.Severity == FindingSeverity.Warning));
        Assert.Equal(1, findings.Count(finding => finding.Severity == FindingSeverity.Info));
        Assert.DoesNotContain(findings, finding => finding.Severity == FindingSeverity.Error);
    }

    private sealed class FixedOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
