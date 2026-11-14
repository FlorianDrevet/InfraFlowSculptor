using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.GitConnections;

public sealed class AzureReposProvider(
    IHttpClientFactory httpClientFactory,
    IfsAzureCredential ifsAzureCredential,
    IConfiguration configuration) : IGitProvider
{
    private const string AzureDevOpsApiVersion = "7.1";
    private const string AzureDevOpsResourceScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    private const string AssertionAudienceScope = "api://AzureADTokenExchange/.default";
    private const string GitApplicationIdConfigurationKey = "Ifs:Git:ClientId";
    private const string EndpointConfigurationKey = "Ifs:Git:AzureDevOps:Endpoint";

    public GitProviderKind Key => GitProviderKind.AzureRepos;

    public string DisplayName => "Azure DevOps";

    public async Task<IReadOnlyCollection<GitRepositoryInfo>> GetRepositoriesAsync(
        GitConnection connection,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.ProviderKind != Key)
        {
            throw new ArgumentException("The connection does not belong to Azure Repos.", nameof(connection));
        }

        var accessToken = connection.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken
            ? transientAccessToken
            : await GetFederatedAccessTokenAsync(connection.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("A usable Azure Repos credential is required.");
        }

        var endpoint = configuration[EndpointConfigurationKey] ?? "https://dev.azure.com";
        if (!Uri.TryCreate($"{endpoint.TrimEnd('/')}/", UriKind.Absolute, out var endpointUri)
            || (endpointUri.Scheme != Uri.UriSchemeHttps && !endpointUri.IsLoopback)
            || !string.IsNullOrEmpty(endpointUri.UserInfo)
            || !string.IsNullOrEmpty(endpointUri.Query)
            || !string.IsNullOrEmpty(endpointUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{EndpointConfigurationKey} must be an HTTPS endpoint or a loopback HTTP endpoint for local tests.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(
                endpointUri,
                $"{Uri.EscapeDataString(connection.Organization)}/_apis/git/repositories?api-version={AzureDevOpsApiVersions.GitRepositories}"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (connection.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken)
        {
            var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await httpClientFactory.CreateClient(nameof(AzureReposProvider))
            .SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<AzureRepositoriesPayload>(
            content,
            cancellationToken: cancellationToken);
        return payload?.Value.Select(repository => new GitRepositoryInfo(
            repository.Id,
            repository.Name,
            repository.DefaultBranch ?? "main")).ToArray() ?? [];
    }

    private async Task<string> GetFederatedAccessTokenAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Federated Azure Repos authentication requires a tenant identifier.");
        }

        var clientId = configuration[GitApplicationIdConfigurationKey];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException($"{GitApplicationIdConfigurationKey} is required for federated Git access.");
        }

        var assertion = await ifsAzureCredential.GetTokenAsync(
            new TokenRequestContext([AssertionAudienceScope]),
            cancellationToken);
        var clientAssertion = new ClientAssertionCredential(
            tenantId.Value.ToString("D"),
            clientId,
            _ => Task.FromResult(assertion.Token));
        var token = await clientAssertion.GetTokenAsync(
            new TokenRequestContext([AzureDevOpsResourceScope]),
            cancellationToken);
        return token.Token;
    }

    private sealed record AzureRepositoriesPayload(
        [property: JsonPropertyName("value")] IReadOnlyList<AzureRepositoryPayload> Value);

    private sealed record AzureRepositoryPayload(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("defaultBranch")] string? DefaultBranch);
}
