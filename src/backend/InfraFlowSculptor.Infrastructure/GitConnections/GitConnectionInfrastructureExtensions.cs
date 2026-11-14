using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace InfraFlowSculptor.Infrastructure.GitConnections;

public static class GitConnectionInfrastructureExtensions
{
    public static IServiceCollection AddGitConnectionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.TryAddSingleton<IfsAzureCredential>();
        services.TryAddSingleton<TokenCredential>(provider => provider.GetRequiredService<IfsAzureCredential>());
        services.TryAddSingleton<SecretClient>(provider =>
        {
            var keyVaultEndpoint = configuration.GetConnectionString("keyvault")
                ?? configuration["Azure:keyvault:Endpoint"];
            if (string.IsNullOrWhiteSpace(keyVaultEndpoint)
                || !Uri.TryCreate(keyVaultEndpoint, UriKind.Absolute, out var vaultUri))
            {
                throw new InvalidOperationException(
                    "A Key Vault endpoint is required in ConnectionStrings:keyvault or Azure:keyvault:Endpoint.");
            }

            return new SecretClient(
                vaultUri,
                provider.GetRequiredService<TokenCredential>(),
                new SecretClientOptions { DisableChallengeResourceVerification = true });
        });
        services.TryAddSingleton<ISecretStore, AzureKeyVaultSecretStore>();

        services.AddHttpClient(nameof(AzureReposProvider), client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddTransient<AzureReposProvider>();
        services.AddTransient<IGitProvider>(provider => provider.GetRequiredService<AzureReposProvider>());
        if (environment.IsDevelopment() && configuration.GetValue<bool>("Ifs:Development:EnableGitea"))
        {
            services.AddHttpClient(nameof(GiteaProvider), client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddTransient<GiteaProvider>();
            services.AddTransient<IGitProvider>(provider => provider.GetRequiredService<GiteaProvider>());
        }

        services.TryAddScoped<GitProviderRegistry>();
        return services;
    }
}
