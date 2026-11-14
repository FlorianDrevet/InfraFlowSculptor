using System.Text;
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
}
