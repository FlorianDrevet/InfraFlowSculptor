using Azure.Core;
using Azure.Identity;

namespace InfraFlowSculptor.Infrastructure.Azure;

public sealed class IfsAzureCredential : TokenCredential
{
    private readonly TokenCredential _innerCredential;

    public IfsAzureCredential()
        : this(CreateCredential(
            Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"),
            Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")))
    {
    }

    internal IfsAzureCredential(TokenCredential innerCredential)
    {
        _innerCredential = innerCredential;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        _innerCredential.GetToken(requestContext, cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        _innerCredential.GetTokenAsync(requestContext, cancellationToken);

    private static TokenCredential CreateCredential(string? identityEndpoint, string? clientId)
    {
        if (!string.IsNullOrWhiteSpace(identityEndpoint))
        {
            return string.IsNullOrWhiteSpace(clientId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
        }

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
    }
}
