using System.Reflection;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Domain.Tests.GitConnections;

public sealed class GitConnectionAggregateTests
{
    [Fact]
    public void GitConnectionAggregateIsPartOfTheDomainModel()
    {
        var aggregate = typeof(OrganizationRole).Assembly.GetType(
            "InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate",
            throwOnError: false);

        Assert.NotNull(aggregate);
        Assert.True(aggregate!.IsSealed);
        Assert.Equal("GitConnectionId", aggregate.BaseType?.GenericTypeArguments.Single().Name);
        Assert.NotNull(aggregate.GetMethod("Create", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void CreateRequiresExpiryForFallbackToken()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");

        var result = GitConnection.Create(
            GitConnectionId.New(TimeProvider.System),
            new OrganizationId(Guid.NewGuid()),
            GitProviderKind.AzureRepos,
            "test",
            "contoso",
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            null,
            "git-connections/test",
            now);

        Assert.True(result.IsError);
        Assert.Equal("VAL-PUB-CONNEXION", result.FirstError.Code);
    }

    [Fact]
    public void RepositoryAccessDefaultsClosedAndCanBeOpenedForSelectedProjects()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);
        var project = new ProjectId(Guid.NewGuid());
        var otherProject = new ProjectId(Guid.NewGuid());
        const string repositoryId = "contoso/shop";
        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create(repositoryId, "shop", "main")]).IsError);

        Assert.False(connection.CanAccessRepository(repositoryId, project));
        Assert.False(connection.OpenRepositoryToProjects(repositoryId, [project]).IsError);

        Assert.True(connection.CanAccessRepository(repositoryId, project));
        Assert.False(connection.CanAccessRepository(repositoryId, otherProject));
    }

    [Fact]
    public void OpeningRepositoryToAllProjectsAllowsEveryProject()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);
        var project = new ProjectId(Guid.NewGuid());
        const string repositoryId = "contoso/shop";
        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create(repositoryId, "shop", "main")]).IsError);

        Assert.False(connection.OpenRepositoryToAllProjects(repositoryId).IsError);

        Assert.True(connection.CanAccessRepository(repositoryId, project));
    }

    [Fact]
    public void RefreshingRepositoriesAndChangingAccessAdvancesVersionOnceForTheCommand()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);
        var initialVersion = connection.Version;
        const string repositoryId = "contoso/shop";

        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create(repositoryId, "shop", "main")]).IsError);
        Assert.False(connection.OpenRepositoryToAllProjects(repositoryId).IsError);

        Assert.Equal(initialVersion + 1, connection.Version);
    }

    [Fact]
    public void RefreshRepositoriesAdvancesVersionAndPreservesProjectAccess()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);
        var project = new ProjectId(Guid.NewGuid());
        const string repositoryId = "contoso/shop";
        Assert.False(connection.ReplaceRepositories([
            GitConnectionRepository.Create(repositoryId, "shop", "main")]).IsError);
        Assert.False(connection.OpenRepositoryToProjects(repositoryId, [project]).IsError);
        var versionBeforeRefresh = connection.Version;

        var result = connection.RefreshRepositories([
            GitConnectionRepository.Create(repositoryId, "shop renamed", "main")]);

        Assert.False(result.IsError);
        Assert.Equal(versionBeforeRefresh + 1, connection.Version);
        Assert.True(connection.CanAccessRepository(repositoryId, project));
    }

    [Fact]
    public void ChangingTokenExpiryResetsPreviouslySentAlerts()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);
        Assert.False(connection.MarkExpiryAlertSent(15).IsError);
        Assert.False(connection.MarkExpiryAlertSent(3).IsError);

        var result = connection.UpdateDetails("test", "contoso", null, now.AddDays(12), connection.SecretName, now);

        Assert.False(result.IsError);
        Assert.False(connection.Expiry15DayAlertSent);
        Assert.False(connection.Expiry3DayAlertSent);
    }

    [Fact]
    public void UpdatingFallbackTokenCanReplaceItsSecretReference()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var connection = CreateConnection(now);

        var result = connection.UpdateDetails(
            "test",
            "contoso",
            null,
            now.AddDays(12),
            "git-conn-replacement-version",
            now);

        Assert.False(result.IsError);
        Assert.Equal("git-conn-replacement-version", connection.SecretName);
    }

    private static GitConnection CreateConnection(DateTimeOffset now) =>
        GitConnection.Create(
            GitConnectionId.New(TimeProvider.System),
            new OrganizationId(Guid.NewGuid()),
            GitProviderKind.AzureRepos,
            "test",
            "contoso",
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(10),
            "git-connections/test",
            now).Value;
}
