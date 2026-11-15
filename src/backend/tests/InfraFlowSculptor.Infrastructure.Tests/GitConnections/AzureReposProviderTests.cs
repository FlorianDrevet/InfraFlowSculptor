using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.GitConnections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.Tests.GitConnections;

public sealed class AzureReposProviderTests
{
    private const string BaseSha = "1111111111111111111111111111111111111111";
    private const string CommitSha = "2222222222222222222222222222222222222222";

    [Fact]
    public async Task GetRepositoriesUsesRecordedResponseAndSendsPatOnlyAsBasicAuthorization()
    {
        const string accessToken = "test-pat-never-log-or-persist";
        const string organization = "contoso-it";
        using var server = WireMockServer.Start();
        var recordingPath = Path.Combine(
            AppContext.BaseDirectory,
            "GitConnections",
            "Recordings",
            "azure-repos-list-repositories.json");
        var recording = await File.ReadAllTextAsync(recordingPath);
        var expectedAuthorization = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}"))}";
        server.Given(Request.Create()
                .WithPath($"/{organization}/_apis/git/repositories")
                .WithParam("api-version", AzureDevOpsApiVersions.GitRepositories)
                .WithHeader("Authorization", expectedAuthorization)
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(recording));

        var services = new ServiceCollection();
        services.AddHttpClient(nameof(AzureReposProvider));
        using var serviceProvider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ifs:Git:AzureDevOps:Endpoint"] = server.Url
            })
            .Build();
        var provider = new AzureReposProvider(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            new IfsAzureCredential(),
            configuration);
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var connection = GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            GitProviderKind.AzureRepos,
            "Test",
            organization,
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(30),
            "git-conn-test",
            now).Value;

        var repositories = await provider.GetRepositoriesAsync(connection, accessToken, CancellationToken.None);

        Assert.Equal(2, repositories.Count);
        Assert.Collection(
            repositories,
            repository =>
            {
                Assert.Equal("00000000-0000-4000-8000-000000000001", repository.ProviderRepositoryId);
                Assert.Equal("shop-infra", repository.Name);
                Assert.Equal("refs/heads/main", repository.DefaultBranch);
            },
            repository =>
            {
                Assert.Equal("00000000-0000-4000-8000-000000000002", repository.ProviderRepositoryId);
                Assert.Equal("shop-app", repository.Name);
                Assert.Equal("refs/heads/trunk", repository.DefaultBranch);
            });
        Assert.Single(server.LogEntries);
    }

    [Fact]
    public async Task GetBranchReadsRefAndCommitMessageWithTheFallbackToken()
    {
        const string accessToken = "test-publication-pat";
        using var server = WireMockServer.Start();
        server.Given(Request.Create()
                .WithPath("/contoso-it/_apis/git/repositories/repository-id/refs")
                .WithParam("filter", "heads/main")
                .WithParam("api-version", "7.1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""
                {"value":[{"name":"refs/heads/main","objectId":"{{BaseSha}}"}]}
                """));
        server.Given(Request.Create()
                .WithPath($"/contoso-it/_apis/git/repositories/repository-id/commits/{BaseSha}")
                .WithParam("api-version", "7.1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""
                {"commitId":"{{BaseSha}}","comment":"Initial commit"}
                """));
        var provider = CreateProvider(server.Url!);

        var branch = await provider.GetBranchAsync(
            CreateConnection(),
            "repository-id",
            "main",
            accessToken,
            CancellationToken.None);

        var branchInfo = Assert.IsType<GitBranchInfo>(branch);
        Assert.Equal("main", branchInfo.Name);
        Assert.Equal(BaseSha, branchInfo.CommitSha);
        Assert.Equal("Initial commit", branchInfo.CommitMessage);
        Assert.Equal(2, server.LogEntries.Count);
        Assert.All(server.LogEntries, entry => Assert.Equal(
            $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}"))}",
            entry.RequestMessage!.Headers!["Authorization"].Single()));
    }

    [Fact]
    public async Task CreateCommitUsesOneAtomicPushForAllFilesAndExpectedBase()
    {
        const string accessToken = "test-publication-pat";
        using var server = WireMockServer.Start();
        server.Given(Request.Create()
                .WithPath("/contoso-it/_apis/git/repositories/repository-id/pushes")
                .WithParam("api-version", "7.1")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201).WithBody($$"""
                {"commits":[{"commitId":"{{CommitSha}}","comment":"IFS revision"}]}
                """));
        var provider = CreateProvider(server.Url!);

        var commitSha = await provider.CreateCommitAsync(
            CreateConnection(),
            "repository-id",
            "ifs/shop/revision-2",
            BaseSha,
            [
                new GitCommitChange("orders/infra/main.bicep", "resource source", GitCommitChangeKind.Add),
                new GitCommitChange(".ifs/manifest.json", "{}", GitCommitChangeKind.Add),
                new GitCommitChange("orders/infra/removed.bicep", null, GitCommitChangeKind.Delete)
            ],
            "IFS revision 2\n\nPublished-by: Test Author",
            accessToken,
            CancellationToken.None);

        Assert.Equal(CommitSha, commitSha);
        using var body = JsonDocument.Parse(server.LogEntries.Single().RequestMessage!.Body!);
        var root = body.RootElement;
        Assert.Equal("refs/heads/ifs/shop/revision-2", root.GetProperty("refUpdates")[0].GetProperty("name").GetString());
        Assert.Equal(BaseSha, root.GetProperty("refUpdates")[0].GetProperty("oldObjectId").GetString());
        var commits = root.GetProperty("commits");
        Assert.Single(commits.EnumerateArray());
        Assert.Equal(BaseSha, commits[0].GetProperty("parents")[0].GetString());
        Assert.Equal(3, commits[0].GetProperty("changes").GetArrayLength());
        Assert.Equal("/orders/infra/removed.bicep", commits[0].GetProperty("changes")[2].GetProperty("item").GetProperty("path").GetString());
    }

    private static AzureReposProvider CreateProvider(string endpoint)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(AzureReposProvider));
        var serviceProvider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ifs:Git:AzureDevOps:Endpoint"] = endpoint
            })
            .Build();
        return new AzureReposProvider(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            new IfsAzureCredential(),
            configuration);
    }

    private static GitConnection CreateConnection()
    {
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        return GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            GitProviderKind.AzureRepos,
            "Test",
            "contoso-it",
            null,
            GitConnectionAuthenticationMode.PersonalAccessToken,
            now.AddDays(30),
            "git-conn-test",
            now).Value;
    }
}
