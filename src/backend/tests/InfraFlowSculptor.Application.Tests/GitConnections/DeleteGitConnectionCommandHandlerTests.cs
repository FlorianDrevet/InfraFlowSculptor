using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.Tests.GitConnections;

public sealed class DeleteGitConnectionCommandHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_git_connections_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_git_connections_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task DeleteConnectionUsedByPublicationPlanReturnsConflictWithProjectNames()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
        dbContext.GitConnectionPublicationUses.Add(
            new GitConnectionPublicationUse(organizationId, connection.Id, project.Id));
        await dbContext.SaveChangesAsync();
        var jobDispatcher = Substitute.For<IJobDispatcher>();
        var handler = new DeleteGitConnectionCommandHandler(dbContext, currentOrganization, jobDispatcher);

        var result = await handler.Handle(
            new DeleteGitConnectionCommand(organizationId.Value, connection.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("CONFLICT_GIT_CONNECTION_IN_USE", result.FirstError.Code);
        Assert.Contains("Boutique en ligne", result.FirstError.Description, StringComparison.Ordinal);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(connection).State);
        jobDispatcher.DidNotReceiveWithAnyArgs().Enqueue<DeleteGitConnectionSecretJob>(default, default, default!);
    }

    [Fact]
    public async Task DeleteUnusedFallbackConnectionQueuesSecretDeletionInNotificationOutbox()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, _, connection) = CreateScenario(
            organizationId,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            "git-conn-test");
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.GitConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        var jobDispatcher = Substitute.For<IJobDispatcher>();
        var handler = new DeleteGitConnectionCommandHandler(dbContext, currentOrganization, jobDispatcher);

        var result = await handler.Handle(
            new DeleteGitConnectionCommand(organizationId.Value, connection.Id.Value),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(EntityState.Deleted, dbContext.Entry(connection).State);
        jobDispatcher.Received(1).Enqueue(
            JobQueue.Notifications,
            organizationId,
            Arg.Is<DeleteGitConnectionSecretJob>(job => job.SecretName == "git-conn-test"));
    }

    private IfsDbContext CreateDbContext(ICurrentOrganization currentOrganization)
    {
        var options = new DbContextOptionsBuilder<IfsDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new IfsDbContext(options, currentOrganization);
    }

    private static (
        UserProfile Profile,
        OrganizationAggregate Organization,
        ProjectAggregate Project,
        GitConnection Connection)
        CreateScenario(
            OrganizationId organizationId,
            GitConnectionAuthenticationMode authenticationMode = GitConnectionAuthenticationMode.DevelopmentToken,
            string? secretName = null)
    {
        var now = TimeProvider.System.GetUtcNow();
        var creator = UserProfile.Create(
            UserProfileId.New(TimeProvider.System),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            "alice@contoso.example",
            now).Value;
        var organization = OrganizationAggregate.Create(organizationId, "Contoso", creator.Id).Value;
        var project = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            organizationId,
            "Boutique en ligne",
            "shop",
            creator.Id).Value;
        var connection = GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            organizationId,
            GitProviderKind.Gitea,
            "Gitea local",
            "contoso",
            null,
            authenticationMode,
            authenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken ? now.AddDays(10) : null,
            secretName,
            now).Value;
        return (creator, organization, project, connection);
    }

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id => id;
    }
}
