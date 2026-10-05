using Azure.Core;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;

namespace InfraFlowSculptor.Infrastructure.Tests.Azure;

public sealed class AzureConnectionResolverTests
{
    private static readonly Uri AzureEndpoint = new("https://ifs.blob.core.windows.net");

    [Fact]
    public void ConnectionStringSelectsLocalModeAndIsPreserved()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var result = AzureConnectionResolver.Resolve(configuration, "blobs", new StubTokenCredential());

        Assert.Equal(AzureConnectionMode.ConnectionString, result.Mode);
        Assert.Equal("UseDevelopmentStorage=true", result.ConnectionString);
        Assert.Null(result.Endpoint);
    }

    [Fact]
    public void EndpointWithoutConnectionStringSelectsIdentityMode()
    {
        var credential = new StubTokenCredential();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azure:blobs:Endpoint"] = AzureEndpoint.ToString()
            })
            .Build();

        var result = AzureConnectionResolver.Resolve(configuration, "blobs", credential);

        Assert.Equal(AzureConnectionMode.ManagedIdentity, result.Mode);
        Assert.Equal(AzureEndpoint, result.Endpoint);
        Assert.Same(credential, result.Credential);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyConnectionStringIsTreatedAsAbsent(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:blobs"] = connectionString,
                ["Azure:blobs:Endpoint"] = AzureEndpoint.ToString()
            })
            .Build();

        var result = AzureConnectionResolver.Resolve(configuration, "blobs", new StubTokenCredential());

        Assert.Equal(AzureConnectionMode.ManagedIdentity, result.Mode);
        Assert.Equal(AzureEndpoint, result.Endpoint);
    }

    [Fact]
    public void MissingConnectionStringAndEndpointFailsWithBothConfigurationKeys()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => AzureConnectionResolver.Resolve(configuration, "blobs", new StubTokenCredential()));

        Assert.Contains("ConnectionStrings:blobs", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Azure:blobs:Endpoint", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
