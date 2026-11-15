using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.Tests.GitConnections;

public sealed class CreateGitConnectionCommandHandlerTests
{
    [Fact]
    public async Task CreateFallbackConnectionStoresPatInSecretStoreAndOnlyReferenceInTrackedEntities()
    {
        const string token = "test-pat-must-not-enter-db-or-logs";
        var organizationId = new OrganizationId(Guid.NewGuid());
        var connectionId = Guid.NewGuid();
        var currentOrganization = Substitute.For<ICurrentOrganization>();
        currentOrganization.Id.Returns(organizationId);
        var dbContext = new IfsDbContext(
            new DbContextOptionsBuilder<IfsDbContext>()
                .UseNpgsql("Host=localhost;Database=ifs_test;Username=ifs;Password=ifs_test")
                .Options,
            currentOrganization);
        var secretStore = Substitute.For<ISecretStore>();
        secretStore.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var rollbackActions = Substitute.For<ITransactionRollbackActions>();
        Func<CancellationToken, Task>? rollbackAction = null;
        rollbackActions
            .When(actions => actions.Register(Arg.Any<Func<CancellationToken, Task>>()))
            .Do(call => rollbackAction = call.Arg<Func<CancellationToken, Task>>());
        var provider = new RecordingGitProvider();
        var handler = new CreateGitConnectionCommandHandler(
            dbContext,
            currentOrganization,
            new GitProviderRegistry([provider]),
            secretStore,
            rollbackActions,
            TimeProvider.System);
        var command = new CreateGitConnectionCommand(
            organizationId.Value,
            connectionId,
            GitProviderKind.AzureRepos,
            "Recette",
            "contoso-test",
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            DateTimeOffset.UtcNow.AddDays(10),
            token);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsError);
        var persistedConnection = Assert.Single(dbContext.ChangeTracker.Entries<GitConnection>()).Entity;
        var secretName = Assert.IsType<string>(persistedConnection.SecretName);
        Assert.StartsWith($"git-conn-{connectionId:N}-", secretName, StringComparison.Ordinal);
        var trackedValues = dbContext.ChangeTracker.Entries()
            .SelectMany(entry => entry.Properties)
            .Select(property => property.CurrentValue?.ToString())
            .ToArray();
        Assert.DoesNotContain(token, trackedValues);
        await secretStore.Received(1).SetAsync(
            secretName,
            token,
            Arg.Any<CancellationToken>());
        Assert.NotNull(rollbackAction);

        await rollbackAction!(CancellationToken.None);

        await secretStore.Received(1).DeleteAsync(secretName, Arg.Any<CancellationToken>());
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
}
