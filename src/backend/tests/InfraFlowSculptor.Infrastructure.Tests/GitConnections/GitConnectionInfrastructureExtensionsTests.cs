using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.GitConnections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace InfraFlowSculptor.Infrastructure.Tests.GitConnections;

public sealed class GitConnectionInfrastructureExtensionsTests
{
    [Fact]
    public void SecretClientDisablesChallengeVerificationOnlyForDevelopmentEmulator()
    {
        var emulatorConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:keyvault"] = "https://localhost:8443"
            })
            .Build();
        var cloudConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azure:keyvault:Endpoint"] = "https://ifs.vault.azure.net"
            })
            .Build();

        var emulatorOptions = GitConnectionInfrastructureExtensions.CreateSecretClientOptions(
            emulatorConfiguration,
            new TestHostEnvironment("Development"));
        var cloudOptions = GitConnectionInfrastructureExtensions.CreateSecretClientOptions(
            cloudConfiguration,
            new TestHostEnvironment("Production"));

        Assert.True(emulatorOptions.DisableChallengeResourceVerification);
        Assert.False(cloudOptions.DisableChallengeResourceVerification);
    }

    [Fact]
    public void RegisteredDevelopmentProvidersResolveFromTheServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ifs:Development:EnableGitea"] = "true"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddGitConnectionInfrastructure(configuration, new TestHostEnvironment("Development"));
        using var serviceProvider = services.BuildServiceProvider();

        var providers = serviceProvider.GetServices<IGitProvider>().ToArray();

        Assert.Collection(
            providers,
            provider => Assert.Equal(GitProviderKind.AzureRepos, provider.Key),
            provider => Assert.Equal(GitProviderKind.Gitea, provider.Key));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "InfraFlowSculptor.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
