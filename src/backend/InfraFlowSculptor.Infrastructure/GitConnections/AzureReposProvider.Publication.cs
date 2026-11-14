using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.GitConnections;

public sealed partial class AzureReposProvider
{
    private const string RefApiVersion = "7.1";
    private const string ItemApiVersion = "7.1";
    private const string PushApiVersion = "7.1";
    private const string PullRequestApiVersion = "7.1";
    private const string ZeroGitSha = "0000000000000000000000000000000000000000";

    public async Task<GitBranchInfo?> GetBranchAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var normalizedBranch = NormalizeBranch(branchName);
        var query = $"filter={EscapeQuery(string.Concat("heads/", normalizedBranch))}&api-version={RefApiVersion}";
        var refs = await SendJsonAsync<AzureRefsPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            "refs",
            query,
            transientAccessToken,
            null,
            cancellationToken);
        var reference = refs?.Value.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, string.Concat("refs/heads/", normalizedBranch), StringComparison.Ordinal));
        if (reference is null || string.IsNullOrWhiteSpace(reference.ObjectId))
        {
            return null;
        }

        var commit = await SendJsonAsync<AzureCommitPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"commits/{Uri.EscapeDataString(reference.ObjectId)}",
            $"api-version={RefApiVersion}",
            transientAccessToken,
            null,
            cancellationToken);
        return new GitBranchInfo(normalizedBranch, reference.ObjectId, commit?.Comment ?? string.Empty);
    }

    public async Task<IReadOnlyCollection<string>> GetTreePathsAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var query = string.Join('&',
            "recursionLevel=Full",
            "scopePath=%2F",
            $"versionDescriptor.version={EscapeQuery(NormalizeBranch(branchName))}",
            "versionDescriptor.versionType=branch",
            $"api-version={ItemApiVersion}");
        var items = await SendJsonAsync<AzureItemsPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            "items",
            query,
            transientAccessToken,
            null,
            cancellationToken);
        return items?.Value
            .Where(item => string.Equals(item.GitObjectType, "blob", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Path.TrimStart('/'))
            .Where(path => path.Length > 0)
            .ToArray() ?? [];
    }

    public async Task<string?> GetFileContentAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string path,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeRepositoryPath(path);
        var query = string.Join('&',
            $"path={EscapeQuery(string.Concat("/", normalizedPath))}",
            "includeContent=true",
            $"versionDescriptor.version={EscapeQuery(NormalizeBranch(branchName))}",
            "versionDescriptor.versionType=branch",
            "api-version=7.1",
            "$format=text");
        using var request = await CreateAuthorizedRequestAsync(
            connection,
            repositoryId,
            HttpMethod.Get,
            "items",
            query,
            transientAccessToken,
            null,
            cancellationToken);
        using var response = await httpClientFactory.CreateClient(nameof(AzureReposProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task CreateBranchAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string baseCommitSha,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var normalizedBranch = NormalizeBranch(branchName);
        var existing = await GetBranchAsync(connection, repositoryId, normalizedBranch, transientAccessToken, cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.CommitSha, baseCommitSha, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException("The source branch already points to a different commit.");
        }

        var body = new[]
        {
            new { name = string.Concat("refs/heads/", normalizedBranch), oldObjectId = ZeroGitSha, newObjectId = baseCommitSha }
        };
        await SendAsync(
            connection,
            repositoryId,
            HttpMethod.Post,
            "refs",
            $"api-version={RefApiVersion}",
            transientAccessToken,
            body,
            cancellationToken);
    }

    public async Task<string> CreateCommitAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string expectedBaseSha,
        IReadOnlyCollection<GitCommitChange> changes,
        string message,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            throw new ArgumentException("A commit must contain at least one file change.", nameof(changes));
        }

        var commitChanges = changes.Select(change => new
        {
            changeType = change.Kind switch
            {
                GitCommitChangeKind.Add => "add",
                GitCommitChangeKind.Edit => "edit",
                GitCommitChangeKind.Delete => "delete",
                _ => throw new InvalidOperationException("The Git change kind is not supported.")
            },
            item = new { path = string.Concat("/", NormalizeRepositoryPath(change.Path)) },
            newContent = change.Kind == GitCommitChangeKind.Delete || change.Content is null
                ? null
                : new { content = change.Content, contentType = "rawtext" }
        }).ToArray();
        var pushBody = new
        {
            refUpdates = new[]
            {
                new { name = string.Concat("refs/heads/", NormalizeBranch(branchName)), oldObjectId = expectedBaseSha }
            },
            commits = new[]
            {
                new { comment = message, parents = new[] { expectedBaseSha }, changes = commitChanges }
            }
        };
        var result = await SendJsonAsync<AzurePushPayload>(
            connection,
            repositoryId,
            HttpMethod.Post,
            "pushes",
            $"api-version={PushApiVersion}",
            transientAccessToken,
            pushBody,
            cancellationToken);
        var commitSha = result is { Commits.Count: > 0 } ? result.Commits[0].CommitId : null;
        return string.IsNullOrWhiteSpace(commitSha)
            ? throw new InvalidOperationException("Azure Repos did not return the commit created by the push.")
            : commitSha;
    }

    public async Task<GitPullRequestInfo?> GetOpenPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string sourceBranch,
        string targetBranch,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var query = string.Join('&',
            $"searchCriteria.sourceRefName={EscapeQuery(ToRefName(sourceBranch))}",
            $"searchCriteria.targetRefName={EscapeQuery(ToRefName(targetBranch))}",
            "searchCriteria.status=active",
            $"api-version={PullRequestApiVersion}");
        var payload = await SendJsonAsync<AzurePullRequestsPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            "pullrequests",
            query,
            transientAccessToken,
            null,
            cancellationToken);
        return payload is null || payload.Value.Count == 0 ? null : ToPullRequest(payload.Value[0]);
    }

    public async Task<GitPullRequestInfo> CreateOrUpdatePullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string? existingPullRequestId,
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        IReadOnlyCollection<string> reviewers,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var reviewerItems = reviewers
            .Where(reviewer => Guid.TryParse(reviewer, out _))
            .Select(reviewer => new AzurePullRequestReviewer(reviewer, IsRequired: false))
            .ToArray();
        var body = new AzurePullRequestWritePayload(
            ToRefName(sourceBranch),
            ToRefName(targetBranch),
            title,
            description,
            reviewerItems);
        var method = existingPullRequestId is null ? HttpMethod.Post : HttpMethod.Patch;
        var path = existingPullRequestId is null
            ? "pullrequests"
            : $"pullrequests/{Uri.EscapeDataString(existingPullRequestId)}";
        var result = await SendJsonAsync<AzurePullRequestPayload>(
            connection,
            repositoryId,
            method,
            path,
            $"api-version={PullRequestApiVersion}",
            transientAccessToken,
            body,
            cancellationToken);
        return result is null
            ? throw new InvalidOperationException("Azure Repos did not return the pull request.")
            : ToPullRequest(result);
    }

    public async Task<GitPullRequestInfo> GetPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string pullRequestId,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var payload = await SendJsonAsync<AzurePullRequestPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"pullrequests/{Uri.EscapeDataString(pullRequestId)}",
            $"api-version={PullRequestApiVersion}",
            transientAccessToken,
            null,
            cancellationToken);
        return payload is null
            ? throw new InvalidOperationException("Azure Repos returned an empty pull request response.")
            : ToPullRequest(payload);
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        GitConnection connection,
        string repositoryId,
        HttpMethod method,
        string path,
        string query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.ProviderKind != GitProviderKind.AzureRepos
            || string.IsNullOrWhiteSpace(repositoryId)
            || string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("An Azure Repos connection, repository, and API path are required.");
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

        var uri = new Uri(endpointUri,
            $"{Uri.EscapeDataString(connection.Organization)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/{path}?{query}");
        var request = new HttpRequestMessage(method, uri);
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

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<T?> SendJsonAsync<T>(
        GitConnection connection,
        string repositoryId,
        HttpMethod method,
        string path,
        string query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            connection,
            repositoryId,
            method,
            path,
            query,
            transientAccessToken,
            body,
            cancellationToken);
        using var response = await httpClientFactory.CreateClient(nameof(AzureReposProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task SendAsync(
        GitConnection connection,
        string repositoryId,
        HttpMethod method,
        string path,
        string query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(
            connection,
            repositoryId,
            method,
            path,
            query,
            transientAccessToken,
            body,
            cancellationToken);
        using var response = await httpClientFactory.CreateClient(nameof(AzureReposProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static GitPullRequestInfo ToPullRequest(AzurePullRequestPayload payload) => new(
        payload.PullRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        payload.Links?.Web?.Href ?? payload.Url,
        StripRefPrefix(payload.SourceRefName),
        StripRefPrefix(payload.TargetRefName),
        payload.Status switch
        {
            "active" => GitPullRequestState.Open,
            "completed" => GitPullRequestState.Merged,
            _ => GitPullRequestState.Closed
        },
        payload.LastMergeCommit?.CommitId);

    private static string ToRefName(string branchName) => string.Concat("refs/heads/", NormalizeBranch(branchName));

    private static string NormalizeBranch(string branchName) => StripRefPrefix(branchName).Trim();

    private static string StripRefPrefix(string branchName) => branchName.StartsWith("refs/heads/", StringComparison.Ordinal)
        ? branchName["refs/heads/".Length..]
        : branchName;

    private static string NormalizeRepositoryPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A safe repository-relative path is required.", nameof(path));
        }

        return normalized;
    }

    private static string EscapeQuery(string value) => Uri.EscapeDataString(value);

    private sealed record AzureRefsPayload([property: JsonPropertyName("value")] IReadOnlyList<AzureRefPayload> Value);

    private sealed record AzureRefPayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("objectId")] string ObjectId);

    private sealed record AzureCommitPayload(
        [property: JsonPropertyName("commitId")] string CommitId,
        [property: JsonPropertyName("comment")] string Comment);

    private sealed record AzureItemsPayload([property: JsonPropertyName("value")] IReadOnlyList<AzureItemPayload> Value);

    private sealed record AzureItemPayload(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("gitObjectType")] string GitObjectType);

    private sealed record AzurePushPayload([property: JsonPropertyName("commits")] IReadOnlyList<AzureCommitPayload> Commits);

    private sealed record AzurePullRequestsPayload([property: JsonPropertyName("value")] IReadOnlyList<AzurePullRequestPayload> Value);

    private sealed record AzurePullRequestReviewer(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("isRequired")] bool IsRequired);

    private sealed record AzurePullRequestWritePayload(
        [property: JsonPropertyName("sourceRefName")] string SourceRefName,
        [property: JsonPropertyName("targetRefName")] string TargetRefName,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("reviewers")] IReadOnlyCollection<AzurePullRequestReviewer> Reviewers);

    private sealed record AzurePullRequestPayload(
        [property: JsonPropertyName("pullRequestId")] int PullRequestId,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("sourceRefName")] string SourceRefName,
        [property: JsonPropertyName("targetRefName")] string TargetRefName,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("lastMergeCommit")] AzureCommitReference? LastMergeCommit,
        [property: JsonPropertyName("_links")] AzureLinks? Links);

    private sealed record AzureCommitReference([property: JsonPropertyName("commitId")] string? CommitId);

    private sealed record AzureLinks([property: JsonPropertyName("web")] AzureLinkReference? Web);

    private sealed record AzureLinkReference([property: JsonPropertyName("href")] string? Href);
}
