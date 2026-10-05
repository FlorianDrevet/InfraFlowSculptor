using Azure.Core;
using Microsoft.Extensions.Configuration;

namespace InfraFlowSculptor.Infrastructure.Azure;

public static class AzureConnectionResolver
{
    public static AzureConnection Resolve(
        IConfiguration configuration,
        string name,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(credential);

        var connectionString = configuration.GetConnectionString(name);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new AzureConnection(name, AzureConnectionMode.ConnectionString, connectionString, null, null);
        }

        var endpointValue = configuration[$"Azure:{name}:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpointValue))
        {
            var normalizedEndpoint = endpointValue.Contains("://", StringComparison.Ordinal)
                ? endpointValue
                : $"https://{endpointValue.TrimEnd('/')}";

            if (!Uri.TryCreate(normalizedEndpoint, UriKind.Absolute, out var endpoint)
                || string.IsNullOrWhiteSpace(endpoint.Host))
            {
                throw new InvalidOperationException(
                    $"Azure resource '{name}' has an invalid endpoint in Azure:{name}:Endpoint.");
            }

            return new AzureConnection(name, AzureConnectionMode.ManagedIdentity, null, endpoint, credential);
        }

        throw new InvalidOperationException(
            $"Azure resource '{name}' requires either ConnectionStrings:{name} or Azure:{name}:Endpoint.");
    }
}
