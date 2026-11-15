using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence.Admin;

public sealed class ProjectPurgerTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task PurgeDeletedProjectRemovesGitConnectionPublicationUsesFirst()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var userKey = new UserKey(Guid.NewGuid(), Guid.NewGuid());
        var now = TimeProvider.System.GetUtcNow();
        var profile = UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            userKey,
            "Alice Martin",
            "alice@contoso.example",
            now).Value;
        var organization = OrganizationAggregate.Create(organizationId, "Contoso", profile.Id).Value;
        var project = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            organizationId,
            "Archived project",
            "archived",
            profile.Id).Value;
        Assert.False(project.SoftDelete(now.AddDays(-31)).IsError);
        var connection = GitConnectionAggregate.Create(
            new GitConnectionId(Guid.NewGuid()),
            organizationId,
            GitProviderKind.Gitea,
            "Gitea local",
            "contoso",
            null,
            GitConnectionAuthenticationMode.DevelopmentToken,
            null,
            null,
            now).Value;
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = postgres.CreatePersistenceDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
        dbContext.GitConnectionPublicationUses.Add(new GitConnectionPublicationUse(
            organizationId,
            connection.Id,
            project.Id));
        await dbContext.SaveChangesAsync();
        var purger = new ProjectPurger(dbContext);

        var purged = await purger.PurgeDeletedBeforeAsync(now.AddDays(-30));

        Assert.Equal(1, purged);
        Assert.False(await dbContext.GitConnectionPublicationUses
            .AnyAsync(use => use.ProjectId == project.Id));
        Assert.True(await dbContext.GitConnections
            .AnyAsync(candidate => candidate.Id == connection.Id));
    }

    [Fact]
    public async Task PurgeDoesNotRemoveUsageWhenProjectIsRestoredAtRetentionBoundary()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var userKey = new UserKey(Guid.NewGuid(), Guid.NewGuid());
        var now = TimeProvider.System.GetUtcNow();
        var deletedAt = now.AddDays(-30);
        var profile = UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            userKey,
            "Alice Martin",
            "alice@contoso.example",
            now).Value;
        var organization = OrganizationAggregate.Create(organizationId, "Contoso", profile.Id).Value;
        var project = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            organizationId,
            "Restored project",
            "restored",
            profile.Id).Value;
        Assert.False(project.SoftDelete(deletedAt).IsError);
        var connection = GitConnectionAggregate.Create(
            new GitConnectionId(Guid.NewGuid()),
            organizationId,
            GitProviderKind.Gitea,
            "Gitea local",
            "contoso",
            null,
            GitConnectionAuthenticationMode.DevelopmentToken,
            null,
            null,
            now).Value;
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using (var seedContext = postgres.CreatePersistenceDbContext(currentOrganization))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.UserProfiles.Add(profile);
            seedContext.Organizations.Add(organization);
            seedContext.Projects.Add(project);
            seedContext.GitConnections.Add(connection);
            seedContext.GitConnectionPublicationUses.Add(new GitConnectionPublicationUse(
                organizationId,
                connection.Id,
                project.Id));
            await seedContext.SaveChangesAsync();
        }

        DateTimeOffset purgeCutoff;
        await using (var readContext = postgres.CreatePersistenceDbContext(currentOrganization))
        {
            purgeCutoff = (await readContext.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == project.Id)
                    .Select(candidate => candidate.DeletedAt)
                    .SingleAsync())
                ?? throw new InvalidOperationException("The persisted project deletion timestamp is missing.");
        }

        await using var restoreContext = postgres.CreatePersistenceDbContext(currentOrganization);
        await using var restoreTransaction = await restoreContext.Database.BeginTransactionAsync();
        var trackedProject = await restoreContext.Projects.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == project.Id);
        Assert.False(trackedProject.Restore(purgeCutoff.AddDays(30).AddMilliseconds(-1)).IsError);
        await restoreContext.SaveChangesAsync();

        await using var purgeContext = postgres.CreatePersistenceDbContext(currentOrganization);
        await purgeContext.Database.OpenConnectionAsync();
        var purgeBackendPid = await purgeContext.Database
            .SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"")
            .SingleAsync();
        var purger = new ProjectPurger(purgeContext);
        var purgeTask = purger.PurgeDeletedBeforeAsync(purgeCutoff);
        await using var monitorContext = postgres.CreatePersistenceDbContext(currentOrganization);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        var purgeIsWaitingForProject = false;
        while (DateTimeOffset.UtcNow < deadline && !purgeIsWaitingForProject)
        {
            purgeIsWaitingForProject = await monitorContext.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE pid = {purgeBackendPid}
                        AND wait_event_type = 'Lock'
                ) AS "Value"
                """).SingleAsync();
            if (!purgeIsWaitingForProject)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
        }

        Assert.True(purgeIsWaitingForProject, "The purge query should wait on the locked project row.");
        await restoreTransaction.CommitAsync();
        var purged = await purgeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, purged);
        Assert.True(await restoreContext.GitConnectionPublicationUses
            .AnyAsync(use => use.ProjectId == project.Id));
        Assert.True(await restoreContext.Projects.IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Id == project.Id && candidate.DeletedAt == null));
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id => id;
    }
}
