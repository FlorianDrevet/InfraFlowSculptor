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
