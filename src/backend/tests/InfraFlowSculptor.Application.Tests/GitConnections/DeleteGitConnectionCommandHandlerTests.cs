using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Application.Projects.Publication;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

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
    public async Task DeleteConnectionUsedBySoftDeletedProjectReturnsConflictUntilProjectIsPurged()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        Assert.False(project.SoftDelete(DateTimeOffset.UtcNow).IsError);
        var destinationId = Guid.NewGuid();
        Assert.False(project.ConfigurePublicationPlan(new PublishPlanConfiguration(
            PublishPreset.MonoRepo,
            [new PublishPlanDestinationInput(destinationId, connection.Id.Value, "contoso/shop", string.Empty)],
            [],
            destinationId,
            destinationId,
            destinationId)).IsError);
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
    public async Task DeleteConnectionReferencedByPublishPlanWithoutUsageRecordReturnsConflict()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        var destinationId = Guid.NewGuid();
        var configuredPlan = project.ConfigurePublicationPlan(new PublishPlanConfiguration(
            PublishPreset.MonoRepo,
            [new PublishPlanDestinationInput(destinationId, connection.Id.Value, "contoso/shop", string.Empty)],
            [],
            destinationId,
            destinationId,
            destinationId));
        Assert.False(configuredPlan.IsError);
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
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

    [Fact]
    public async Task DeleteConnectionReferencedByActivePublicationReturnsConflictAfterPlanNoLongerUsesIt()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        var now = DateTimeOffset.UtcNow;
        var revision = RevisionAggregate.Queue(
            new RevisionId(Guid.NewGuid()),
            organizationId,
            project.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            1,
            "test-catalog",
            "Bicep",
            "AzureDevOps",
            new string('a', 64));
        var destinationId = Guid.NewGuid();
        var publication = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            organizationId,
            project.Id,
            revision.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            null,
            [new PublicationTargetInput(
                connection.Id,
                "contoso/shop",
                "shop",
                "main",
                "ifs/shop/revision-1",
                [new PublicationTargetDestinationInput(destinationId, "")],
                [])]).Value;
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
        dbContext.Revisions.Add(revision);
        dbContext.Publications.Add(publication);
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
    public async Task DeleteWaitsForPublicationStartHoldingTheConnectionLock()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var startContext = CreateDbContext(currentOrganization);
        await startContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        var now = DateTimeOffset.UtcNow;
        var revision = RevisionAggregate.Queue(
            new RevisionId(Guid.NewGuid()),
            organizationId,
            project.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            1,
            "test-catalog",
            "Bicep",
            "AzureDevOps",
            new string('a', 64));
        startContext.UserProfiles.Add(profile);
        startContext.Organizations.Add(organization);
        startContext.Projects.Add(project);
        startContext.GitConnections.Add(connection);
        startContext.Revisions.Add(revision);
        await startContext.SaveChangesAsync();

        await using var startTransaction = await startContext.BeginTransactionAsync();
        Assert.True(await startContext.LockGitConnectionForUpdateAsync(connection.Id, CancellationToken.None));
        var publication = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            organizationId,
            project.Id,
            revision.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            null,
            [new PublicationTargetInput(
                connection.Id,
                "contoso/shop",
                "shop",
                "main",
                "ifs/shop/revision-1",
                [new PublicationTargetDestinationInput(Guid.NewGuid(), "")],
                [])]).Value;
        startContext.Publications.Add(publication);
        await startContext.SaveChangesAsync();

        await using var deleteContext = CreateDbContext(currentOrganization);
        await using var deleteTransaction = await deleteContext.BeginTransactionAsync();
        var jobDispatcher = Substitute.For<IJobDispatcher>();
        var handler = new DeleteGitConnectionCommandHandler(deleteContext, currentOrganization, jobDispatcher);
        var deleteTask = handler.Handle(
            new DeleteGitConnectionCommand(organizationId.Value, connection.Id.Value),
            CancellationToken.None).AsTask();

        var completedBeforePublicationCommit = await Task.WhenAny(deleteTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
        Assert.NotSame(deleteTask, completedBeforePublicationCommit);

        await startTransaction.CommitAsync();
        var result = await deleteTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsError);
        Assert.Equal("CONFLICT_GIT_CONNECTION_IN_USE", result.FirstError.Code);
        await deleteTransaction.RollbackAsync();
    }

    [Fact]
    public async Task PublishPlanUpdateWaitsForConnectionDeletionAndRejectsTheMissingConnection()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var deleteContext = CreateDbContext(currentOrganization);
        await deleteContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create("contoso/shop", "shop", "main")]).IsError);
        Assert.False(connection.OpenRepositoryToAllProjects("contoso/shop").IsError);
        deleteContext.UserProfiles.Add(profile);
        deleteContext.Organizations.Add(organization);
        deleteContext.Projects.Add(project);
        deleteContext.GitConnections.Add(connection);
        await deleteContext.SaveChangesAsync();

        await using var deleteTransaction = await deleteContext.BeginTransactionAsync();
        Assert.True(await deleteContext.LockGitConnectionForUpdateAsync(connection.Id, CancellationToken.None));
        deleteContext.GitConnections.Remove(connection);
        await deleteContext.SaveChangesAsync();

        await using var planContext = CreateDbContext(currentOrganization);
        await using var planTransaction = await planContext.BeginTransactionAsync();
        var handler = new SetProjectPublishPlanCommandHandler(planContext);
        var destinationId = Guid.NewGuid();
        var updateTask = handler.Handle(
            new SetProjectPublishPlanCommand(
                project.Id.Value,
                PublishPreset.MonoRepo,
                [new PublishPlanDestinationInput(destinationId, connection.Id.Value, "contoso/shop", string.Empty)],
                [],
                destinationId,
                destinationId,
                destinationId,
                project.Version),
            CancellationToken.None).AsTask();

        var completedBeforeDeletionCommit = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
        Assert.NotSame(updateTask, completedBeforeDeletionCommit);

        await deleteTransaction.CommitAsync();
        var result = await updateTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsError);
        Assert.Equal(PublishValidationCodes.Destination, result.FirstError.Code);
        await planTransaction.RollbackAsync();
    }

    [Fact]
    public async Task PublishPlanUpdateRejectsAssignmentToComponentOutsideTheProject()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create("contoso/shop", "shop", "main")]).IsError);
        Assert.False(connection.OpenRepositoryToAllProjects("contoso/shop").IsError);
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var handler = new SetProjectPublishPlanCommandHandler(dbContext);
        var destinationId = Guid.NewGuid();
        var result = await handler.Handle(
            new SetProjectPublishPlanCommand(
                project.Id.Value,
                PublishPreset.MonoRepo,
                [new PublishPlanDestinationInput(destinationId, connection.Id.Value, "contoso/shop", string.Empty)],
                [new PublishPlanAssignmentInput(Guid.NewGuid(), PublishPart.Infrastructure, destinationId)],
                destinationId,
                destinationId,
                destinationId,
                project.Version),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(PublishValidationCodes.Destination, result.FirstError.Code);
        Assert.Empty(project.PublicationPlan.Assignments);
    }

    [Fact]
    public async Task DeleteConnectionReferencedByCompletedPublicationDoesNotBlockRemoval()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var dbContext = CreateDbContext(currentOrganization);
        await dbContext.Database.EnsureCreatedAsync();
        var (profile, organization, project, connection) = CreateScenario(organizationId);
        var now = DateTimeOffset.UtcNow;
        var revision = RevisionAggregate.Queue(
            new RevisionId(Guid.NewGuid()),
            organizationId,
            project.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            1,
            "test-catalog",
            "Bicep",
            "AzureDevOps",
            new string('a', 64));
        var targetId = Guid.NewGuid();
        var publication = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            organizationId,
            project.Id,
            revision.Id,
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            now,
            null,
            [new PublicationTargetInput(
                connection.Id,
                "contoso/shop",
                "shop",
                "main",
                "ifs/shop/revision-1",
                [new PublicationTargetDestinationInput(targetId, "")],
                [])]).Value;
        var publicationTargetId = publication.Targets.Single().Id;
        Assert.False(publication.MarkTargetPrepared(
            publicationTargetId,
            "main",
            new string('b', 40),
            [],
            requiresConfirmation: false,
            now).IsError);
        Assert.False(publication.BeginWriting().IsError);
        Assert.False(publication.RecordPullRequest(
            publicationTargetId,
            new string('c', 40),
            "1",
            "https://git.example/contoso/shop/pulls/1",
            now).IsError);
        Assert.Equal(PublicationState.Completed, publication.State);
        dbContext.UserProfiles.Add(profile);
        dbContext.Organizations.Add(organization);
        dbContext.Projects.Add(project);
        dbContext.GitConnections.Add(connection);
        dbContext.Revisions.Add(revision);
        dbContext.Publications.Add(publication);
        await dbContext.SaveChangesAsync();
        var jobDispatcher = Substitute.For<IJobDispatcher>();
        var handler = new DeleteGitConnectionCommandHandler(dbContext, currentOrganization, jobDispatcher);

        var result = await handler.Handle(
            new DeleteGitConnectionCommand(organizationId.Value, connection.Id.Value),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(EntityState.Deleted, dbContext.Entry(connection).State);
        await dbContext.SaveChangesAsync();
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
