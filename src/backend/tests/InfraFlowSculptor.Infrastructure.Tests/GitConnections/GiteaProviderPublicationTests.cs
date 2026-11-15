using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.GitConnections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.Tests.GitConnections;

public sealed class GiteaProviderPublicationTests
{
    private const string BaseSha = "1111111111111111111111111111111111111111";
    private const string CommitSha = "2222222222222222222222222222222222222222";
    private const string BlobSha = "3333333333333333333333333333333333333333";

    [Fact]
    public async Task GetBranchReadsGiteasFlatCommitIdAndMessage()
    {
        const string accessToken = "test-gitea-token-never-log-or-persist";
        using var server = WireMockServer.Start();
        server.Given(Request.Create()
                .WithPath("/api/v1/repos/contoso/shop/branches/main")
                .WithHeader("Authorization", $"token {accessToken}")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""
                {
                  "name": "main",
                  "commit": {
                    "id": "{{BaseSha}}",
                    "message": "Initial commit",
                    "url": "http://gitea/api/v1/repos/contoso/shop/git/commits/{{BaseSha}}"
                  }
                }
                """));

        using var fixture = CreateProvider(server, accessToken);

        var branch = await fixture.Provider.GetBranchAsync(
            fixture.Connection,
            "contoso/shop",
            "main",
            fixture.Token,
            CancellationToken.None);

        Assert.NotNull(branch);
        Assert.Equal("main", branch.Name);
        Assert.Equal(BaseSha, branch.CommitSha);
        Assert.Equal("Initial commit", branch.CommitMessage);
        Assert.Single(server.LogEntries);
    }

    [Fact]
    public async Task GetBranchEncodesNestedBranchNameOnce()
    {
        const string accessToken = "test-gitea-token-never-log-or-persist";
        const string branchName = "ifs/r07ref/revision-1";
        using var server = WireMockServer.Start();
        StubBranch(server, accessToken, branchName);
        using var fixture = CreateProvider(server, accessToken);

        var branch = await fixture.Provider.GetBranchAsync(
            fixture.Connection,
            "contoso/shop",
            branchName,
            fixture.Token,
            CancellationToken.None);

        var requestPath = Assert.Single(server.LogEntries).RequestMessage!.Path;
        Assert.Equal("/api/v1/repos/contoso/shop/branches/ifs/r07ref/revision-1", requestPath);
        Assert.NotNull(branch);
        Assert.Equal(branchName, branch.Name);
    }

    [Fact]
    public async Task CreateBranchUsesGiteaBranchesEndpoint()
    {
        const string accessToken = "test-gitea-token-never-log-or-persist";
        const string branchName = "ifs/r07ref/revision-1";
        using var server = WireMockServer.Start();
        server.Given(Request.Create()
                .WithPath("/api/v1/repos/contoso/shop/branches")
                .WithHeader("Authorization", $"token {accessToken}")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201).WithBody("{}"));
        using var fixture = CreateProvider(server, accessToken);

        await fixture.Provider.CreateBranchAsync(
            fixture.Connection,
            "contoso/shop",
            branchName,
            BaseSha,
            fixture.Token,
            CancellationToken.None);

        Assert.Equal(2, server.LogEntries.Count);
        var request = server.LogEntries.Single(entry => entry.RequestMessage?.Method == "POST").RequestMessage!;
        Assert.Equal("POST", request.Method);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(branchName, body.RootElement.GetProperty("new_branch_name").GetString());
        Assert.Equal(BaseSha, body.RootElement.GetProperty("old_ref_name").GetString());
    }

    [Fact]
    public async Task CreateCommitUsesGiteaMultiFileCommitEndpoint()
    {
        const string accessToken = "test-gitea-token-never-log-or-persist";
        const string branchName = "ifs/r07ref/revision-1";
        using var server = WireMockServer.Start();
        StubBranch(server, accessToken, branchName);
        server.Given(Request.Create()
                .WithPath("/api/v1/repos/contoso/shop/contents")
                .WithHeader("Authorization", $"token {accessToken}")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201).WithBody($$"""
                { "commit": { "sha": "{{CommitSha}}" } }
                """));
        using var fixture = CreateProvider(server, accessToken);

        var commitSha = await fixture.Provider.CreateCommitAsync(
            fixture.Connection,
            "contoso/shop",
            branchName,
            BaseSha,
            [new GitCommitChange("orders/infra/main.bicep", "resource source", GitCommitChangeKind.Add)],
            "IFS revision 1",
            fixture.Token,
            CancellationToken.None);

        Assert.Equal(CommitSha, commitSha);
        var request = server.LogEntries.Single(entry => entry.RequestMessage?.Method == "POST").RequestMessage!;
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(branchName, body.RootElement.GetProperty("branch").GetString());
        Assert.Equal("IFS revision 1", body.RootElement.GetProperty("message").GetString());
        var file = Assert.Single(body.RootElement.GetProperty("files").EnumerateArray());
        Assert.Equal("create", file.GetProperty("operation").GetString());
        Assert.Equal("orders/infra/main.bicep", file.GetProperty("path").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("resource source")), file.GetProperty("content").GetString());
    }

    [Fact]
    public async Task CreateCommitReadsExistingBlobShasForUpdatesAndDeletes()
    {
        const string accessToken = "test-gitea-token-never-log-or-persist";
        const string branchName = "ifs/r07ref/revision-1";
        using var server = WireMockServer.Start();
        StubBranch(server, accessToken, branchName);
        foreach (var path in new[] { "orders/infra/update.bicep", "orders/infra/delete.bicep" })
        {
            server.Given(Request.Create()
                    .WithPath($"/api/v1/repos/contoso/shop/contents/{path}")
                    .WithParam("ref", branchName)
                    .WithHeader("Authorization", $"token {accessToken}")
                    .UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""
                    {
                      "type": "file",
                      "encoding": "base64",
                      "content": "",
                      "sha": "{{BlobSha}}"
                    }
                    """));
        }

        server.Given(Request.Create()
                .WithPath("/api/v1/repos/contoso/shop/contents")
                .WithHeader("Authorization", $"token {accessToken}")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201).WithBody($$"""
                { "commit": { "sha": "{{CommitSha}}" } }
                """));
        using var fixture = CreateProvider(server, accessToken);

        await fixture.Provider.CreateCommitAsync(
            fixture.Connection,
            "contoso/shop",
            branchName,
            BaseSha,
            [
                new GitCommitChange("orders/infra/update.bicep", "updated source", GitCommitChangeKind.Edit),
                new GitCommitChange("orders/infra/delete.bicep", null, GitCommitChangeKind.Delete)
            ],
            "IFS revision 1",
            fixture.Token,
            CancellationToken.None);

        var request = server.LogEntries.Single(entry => entry.RequestMessage?.Method == "POST").RequestMessage!;
        using var body = JsonDocument.Parse(request.Body!);
        var files = body.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(2, files.Length);
        Assert.Equal("update", files[0].GetProperty("operation").GetString());
        Assert.Equal(BlobSha, files[0].GetProperty("sha").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("updated source")), files[0].GetProperty("content").GetString());
        Assert.Equal("delete", files[1].GetProperty("operation").GetString());
        Assert.Equal(BlobSha, files[1].GetProperty("sha").GetString());
        Assert.False(files[1].TryGetProperty("content", out _));
    }

    private static ProviderFixture CreateProvider(WireMockServer server, string token)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(GiteaProvider));
        var serviceProvider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ifs:Development:Gitea:Endpoint"] = server.Url
            })
            .Build();
        var provider = new GiteaProvider(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            configuration);
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var connection = GitConnection.Create(
            new GitConnectionId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            GitProviderKind.Gitea,
            "Test",
            "contoso",
            null,
            GitConnectionAuthenticationMode.DevelopmentToken,
            null,
            null,
            now).Value;
        return new ProviderFixture(provider, connection, token, serviceProvider);
    }

    private static void StubBranch(WireMockServer server, string token, string branchName)
    {
        server.Given(Request.Create()
                .WithPath("/api/v1/repos/contoso/shop/branches/ifs/r07ref/revision-1")
                .WithHeader("Authorization", $"token {token}")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody($$"""
                {
                  "name": "{{branchName}}",
                  "commit": { "id": "{{BaseSha}}", "message": "Initial commit" }
                }
                """));
    }

    private sealed class ProviderFixture(
        GiteaProvider provider,
        GitConnection connection,
        string token,
        ServiceProvider serviceProvider) : IDisposable
    {
        public GiteaProvider Provider { get; } = provider;

        public GitConnection Connection { get; } = connection;

        public string Token { get; } = token;

        public void Dispose() => serviceProvider.Dispose();
    }
}
