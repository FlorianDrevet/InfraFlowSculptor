using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.Tests.GitConnections;

public sealed class UpdateGitConnectionCommandHandlerTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task FallbackTokenRotationKeepsOriginalReferenceAndDeletesStagedSecretAfterRollback()
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = new OrganizationId(Guid.NewGuid());
        var owner = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Test Owner",
            $"owner-{Guid.NewGuid():N}@example.test",
            now).Value;
        var organization = OrganizationAggregate.Create(
            organizationId,
            "Secret Rotation Test",
            owner.Id).Value;
        var connectionId = new GitConnectionId(Guid.NewGuid());
        const string originalSecretName = "git-conn-original-version";
        var connection = GitConnection.Create(
            connectionId,
            organizationId,
            GitProviderKind.AzureRepos,
            "Original",
            "contoso",
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(10),
            originalSecretName,
            now).Value;
        Assert.False(connection.ReplaceRepositories(
            [GitConnectionRepository.Create("repo-1", "shop", "main")]).IsError);

        var rollbackActions = new TransactionRollbackActions(NullLogger<TransactionRollbackActions>.Instance);
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var context = postgres.CreatePersistenceDbContext(currentOrganization, rollbackActions);
        await context.Database.EnsureCreatedAsync();
        context.UserProfiles.Add(owner);
        context.Organizations.Add(organization);
        context.GitConnections.Add(connection);
        await context.SaveChangesAsync();

        var secretStore = Substitute.For<ISecretStore>();
        secretStore.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        secretStore.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var jobDispatcher = Substitute.For<IJobDispatcher>();
        var provider = new RecordingGitProvider();
        var handler = new UpdateGitConnectionCommandHandler(
            context,
            currentOrganization,
            new GitProviderRegistry([provider]),
            secretStore,
            jobDispatcher,
            rollbackActions,
            TimeProvider.System);
        await using var transaction = await context.BeginTransactionAsync();

        var result = await handler.Handle(
            new UpdateGitConnectionCommand(
                organizationId.Value,
                connectionId.Value,
                connection.Version,
                "Updated",
                "contoso",
                null,
                now.AddDays(20),
                "replacement-test-token"),
            CancellationToken.None);

        Assert.False(result.IsError);
        var replacementSecretName = Assert.IsType<string>(connection.SecretName);
        Assert.StartsWith($"git-conn-{connectionId.Value:N}-", replacementSecretName, StringComparison.Ordinal);
        Assert.NotEqual(originalSecretName, replacementSecretName);
        await context.SaveChangesAsync();
        await transaction.RollbackAsync(CancellationToken.None);

        await using var observer = postgres.CreatePersistenceDbContext(currentOrganization);
        var persistedConnection = await observer.GitConnections.SingleAsync(candidate => candidate.Id == connectionId);
        Assert.Equal(originalSecretName, persistedConnection.SecretName);
        await secretStore.Received(1).SetAsync(
            replacementSecretName,
            "replacement-test-token",
            Arg.Any<CancellationToken>());
        await secretStore.Received(1).DeleteAsync(replacementSecretName, Arg.Any<CancellationToken>());
        jobDispatcher.Received(1).Enqueue(
            JobQueue.Notifications,
            organizationId,
            Arg.Is<DeleteGitConnectionSecretJob>(job => job.SecretName == originalSecretName));
    }

    private sealed class RecordingGitProvider : IGitProvider
    {
        public GitProviderKind Key => GitProviderKind.AzureRepos;

        public string DisplayName => "Azure DevOps";

        public Task<IReadOnlyCollection<GitRepositoryInfo>> GetRepositoriesAsync(
            GitConnection connection,
            string? transientAccessToken,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<GitRepositoryInfo>>(
                [new GitRepositoryInfo("repo-1", "shop", "main")]);
    }

    private sealed class TestCurrentOrganization(OrganizationId organizationId) : ICurrentOrganization
    {
        public OrganizationId? Id => organizationId;
    }
}
