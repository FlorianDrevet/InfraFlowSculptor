using Azure;
using Azure.Security.KeyVault.Secrets;
using InfraFlowSculptor.Application.Common.Secrets;

namespace InfraFlowSculptor.Infrastructure.Secrets;

public sealed class AzureKeyVaultSecretStore(SecretClient secretClient) : ISecretStore
{
    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await secretClient.GetSecretAsync(name, cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        await secretClient.SetSecretAsync(name, value, cancellationToken);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try
        {
            await secretClient.StartDeleteSecretAsync(name, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
        }
    }
}
