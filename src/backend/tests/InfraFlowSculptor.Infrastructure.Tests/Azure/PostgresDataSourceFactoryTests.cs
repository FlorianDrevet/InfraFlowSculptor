using Azure.Core;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Tests.Azure;

public sealed class PostgresDataSourceFactoryTests
{
    [Fact]
    public async Task AzureConfigurationBuildsWithoutPasswordAndRequestsAnEntraToken()
    {
        var credential = new StubTokenCredential();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azure:ifs:Endpoint"] = "ifs.postgres.database.azure.com",
                ["Azure:ifs:Database"] = "ifs",
                ["Azure:ifs:Username"] = "ifs-api"
            })
            .Build();

        await using var dataSource = PostgresDataSourceFactory.Create(configuration, "ifs", credential);
        var connection = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.True(string.IsNullOrEmpty(connection.Password));
        Assert.Equal("ifs.postgres.database.azure.com", connection.Host);
        Assert.Equal(SslMode.Require, connection.SslMode);

        var requestedScope = await credential.TokenRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PostgresDataSourceFactory.TokenScope, requestedScope);
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public TaskCompletionSource<string> TokenRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            TokenRequested.TrySetResult(requestContext.Scopes.Single());
            return ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
