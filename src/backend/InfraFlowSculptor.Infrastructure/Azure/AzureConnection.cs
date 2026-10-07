using Azure.Core;

namespace InfraFlowSculptor.Infrastructure.Azure;

public sealed record AzureConnection(
    string Name,
    AzureConnectionMode Mode,
    string? ConnectionString,
    Uri? Endpoint,
    TokenCredential? Credential);
