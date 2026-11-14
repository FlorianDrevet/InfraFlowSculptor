using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using Microsoft.Extensions.Configuration;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.GitConnections;

public sealed partial class GiteaProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IGitProvider
{
    private const int PageSize = 50;
    private const string EndpointConfigurationKey = "Ifs:Development:Gitea:Endpoint";
    private const string TokenConfigurationKey = "Gitea:Token";

    public GitProviderKind Key => GitProviderKind.Gitea;

    public string DisplayName => "Gitea (développement)";

    public async Task<IReadOnlyCollection<GitRepositoryInfo>> GetRepositoriesAsync(
        GitConnection connection,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.ProviderKind != Key || connection.AuthenticationMode != GitConnectionAuthenticationMode.DevelopmentToken)
        {
            throw new ArgumentException("The connection is not a development Gitea connection.", nameof(connection));
        }

        var token = transientAccessToken ?? configuration[TokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("The development Gitea token is not configured.");
        }

        var endpoint = configuration[EndpointConfigurationKey] ?? "http://localhost:3000";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{EndpointConfigurationKey} must be an HTTP endpoint.");
        }

        using var client = httpClientFactory.CreateClient(nameof(GiteaProvider));
        client.BaseAddress = endpointUri;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        var repositories = new List<GitRepositoryInfo>();
        for (var page = 1; ; page++)
        {
            var requestUri = $"api/v1/orgs/{Uri.EscapeDataString(connection.Organization)}/repos?limit={PageSize}&page={page}";
            using var response = await client.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<IReadOnlyList<GiteaRepositoryPayload>>(
                content,
                cancellationToken: cancellationToken) ?? [];
            repositories.AddRange(payload.Select(repository => new GitRepositoryInfo(
                repository.FullName,
                repository.Name,
                repository.DefaultBranch ?? "main")));
            if (payload.Count < PageSize)
            {
                return repositories;
            }
        }
    }

    private sealed record GiteaRepositoryPayload(
        [property: JsonPropertyName("full_name")] string FullName,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("default_branch")] string? DefaultBranch);
}
