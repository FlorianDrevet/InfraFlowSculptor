using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class LinkPersistenceTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task LinkAndDefaultWorkspacePersistAsRelationalData()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var creatorId = new UserProfileId(Guid.NewGuid());
        var currentOrganization = new FixedOrganization(organizationId);
        await using var dbContext = postgres.CreatePersistenceDbContext(currentOrganization);
        await dbContext.Database.MigrateAsync();

        var organization = OrganizationAggregate.Create(organizationId, "Link persistence", creatorId).Value;
        var project = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            organizationId,
            "Link persistence",
            "linktest",
            Guid.NewGuid(),
            "",
            "2026.10-pilot",
            creatorId).Value;
        project.AddEnvironment(
            Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1,
            false, [], [], "");
        var component = ComponentAggregate.Create(
            new ComponentId(Guid.NewGuid()),
            project.Id,
            organizationId,
            "Orders",
            "orders",
            "",
            ComponentDeploymentMode.PerEnvironment,
            project.Environments).Value;
        var workspaceResourceId = Guid.NewGuid();
        component.SetDefaultLogAnalyticsResource(workspaceResourceId);
        var sourceResourceId = Guid.NewGuid();
        var targetResourceId = Guid.NewGuid();
        var identityResourceId = Guid.NewGuid();
        var link = LinkAggregate.Create(
            Guid.NewGuid(),
            project.Id,
            organizationId,
            component.Id.Value,
            sourceResourceId,
            component.Id.Value,
            targetResourceId,
            LinkKind.Access,
            LinkIdentityKind.UserAssigned,
            identityResourceId,
            ["Log Analytics Reader"],
            LinkAccessScope.Resource,
            null).Value;
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.Components.Add(component);
        dbContext.Set<LinkAggregate>().Add(link);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var persistedComponent = await dbContext.Components.SingleAsync(candidate => candidate.Id == component.Id);
        var persistedLink = await dbContext.Set<LinkAggregate>().SingleAsync(candidate => candidate.Id == link.Id);

        Assert.Equal(workspaceResourceId, persistedComponent.DefaultLogAnalyticsResourceId);
        Assert.Equal(LinkKind.Access, persistedLink.Kind);
        Assert.Equal(sourceResourceId, persistedLink.SourceResourceId);
        Assert.Equal(targetResourceId, persistedLink.TargetResourceId);
        Assert.Equal(identityResourceId, persistedLink.IdentityResourceId);
        Assert.Equal("Log Analytics Reader", Assert.Single(persistedLink.Roles).Name);
    }

    private sealed class FixedOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
