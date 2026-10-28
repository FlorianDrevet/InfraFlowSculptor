using Azure.Core;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Configuration;

public sealed class AppConfigurationDependencyCheck(
    IConfiguration configuration,
    TokenCredential credential) : IDependencyCheck
{
    public string Name => "appconfig";

    public async Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
    {
        var endpoint = configuration["AZURE_APPCONFIG_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var client = new ConfigurationClient(new Uri(endpoint), credential);
        await client.GetConfigurationSettingAsync("orders:maxItemsPerOrder", cancellationToken: cancellationToken);
        return new DependencyCheckOutcome(true, null);
    }
}
