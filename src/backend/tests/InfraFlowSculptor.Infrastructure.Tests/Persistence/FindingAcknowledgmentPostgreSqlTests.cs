using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class FindingAcknowledgmentPostgreSqlTests(PostgreSqlFixture postgres)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task DuplicateNullDetailKeyIsMappedToAlreadyAcknowledgedConflict()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var ownerId = new UserProfileId(Guid.NewGuid());
        var projectId = new ProjectId(Guid.NewGuid());
        var owner = UserProfile.Create(
            ownerId,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Acknowledgment Owner",
            "acknowledgment-owner@example.test",
            DateTimeOffset.UtcNow).Value;
        var organization = OrganizationAggregate.Create(organizationId, "Acknowledgment Test", ownerId).Value;
        var projectResult = ProjectAggregate.Create(
            projectId,
            organizationId,
            "Acknowledgment Test Project",
            "acktest",
            owner.TenantId,
            string.Empty,
            "2026.10-pilot.3",
            ownerId);
        Assert.False(projectResult.IsError);
        var project = projectResult.Value;
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(organizationId));
        await dbContext.Database.MigrateAsync();
        dbContext.UserProfiles.Add(owner);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var subjectId = Guid.NewGuid();
        dbContext.Set<FindingAcknowledgmentAggregate>().Add(CreateAcknowledgment(
            Guid.NewGuid(), projectId, organizationId, subjectId, ownerId));
        await dbContext.SaveChangesAsync();

        dbContext.Set<FindingAcknowledgmentAggregate>().Add(CreateAcknowledgment(
            Guid.NewGuid(), projectId, organizationId, subjectId, ownerId));

        await Assert.ThrowsAsync<FindingAcknowledgmentConflictException>(() => dbContext.SaveChangesAsync());
    }

    private static FindingAcknowledgmentAggregate CreateAcknowledgment(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid subjectId,
        UserProfileId ownerId) =>
        FindingAcknowledgmentAggregate.Create(
            id,
            projectId,
            organizationId,
            "VAL-LIA-SQL-ADMIN",
            detailKey: null,
            ObjectType.Resource,
            subjectId,
            environmentId: null,
            objectVersion: 1,
            "Reviewed",
            ownerId,
            DateTimeOffset.UtcNow).Value;

    private sealed class FixedOrganization(OrganizationId id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
