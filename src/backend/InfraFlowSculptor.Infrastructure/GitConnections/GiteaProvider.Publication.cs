using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using Microsoft.Extensions.Configuration;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Infrastructure.GitConnections;

public sealed partial class GiteaProvider
{
    public async Task<GitBranchInfo?> GetBranchAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var relativePath = string.Concat("branches/", Uri.EscapeDataString(NormalizeBranch(branchName)));
        var response = await SendJsonAsync<GiteaBranchPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            relativePath,
            null,
            transientAccessToken,
            null,
            cancellationToken,
            missingIsNull: true,
            preserveEscapedBranchName: true);
        if (response?.Commit is not { } commit || string.IsNullOrWhiteSpace(commit.Id))
        {
            return null;
        }

        return new GitBranchInfo(NormalizeBranch(branchName), commit.Id, commit.Message ?? string.Empty);
    }

    public async Task<IReadOnlyCollection<string>> GetTreePathsAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var branch = await GetBranchAsync(connection, repositoryId, branchName, transientAccessToken, cancellationToken);
        if (branch is null)
        {
            return [];
        }

        var tree = await SendJsonAsync<GiteaTreePayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"git/trees/{Uri.EscapeDataString(branch.CommitSha)}",
            "recursive=true",
            transientAccessToken,
            null,
            cancellationToken);
        return tree?.Tree
            .Where(item => string.Equals(item.Type, "blob", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
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
        var payload = await SendJsonAsync<GiteaFilePayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"contents/{normalizedPath}",
            $"ref={Uri.EscapeDataString(NormalizeBranch(branchName))}",
            transientAccessToken,
            null,
            cancellationToken,
            missingIsNull: true);
        if (payload is null)
        {
            return null;
        }

        if (!string.Equals(payload.Type, "file", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(payload.Encoding, "base64", StringComparison.OrdinalIgnoreCase)
            || payload.Content is null)
        {
            throw new InvalidDataException("Gitea returned an unsupported file response.");
        }

        var encoded = string.Concat(payload.Content.Where(character => !char.IsWhiteSpace(character)));
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
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

        await SendAsync(
            connection,
            repositoryId,
            HttpMethod.Post,
            "branches",
            null,
            transientAccessToken,
            new { new_branch_name = normalizedBranch, old_ref_name = baseCommitSha },
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

        var normalizedBranch = NormalizeBranch(branchName);
        var branch = await GetBranchAsync(
            connection,
            repositoryId,
            normalizedBranch,
            transientAccessToken,
            cancellationToken);
        if (branch is null
            || !string.Equals(branch.CommitSha, expectedBaseSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The source branch changed before the publication commit was created.");
        }

        var fileChanges = new List<GiteaChangeFilePayload>(changes.Count);
        foreach (var change in changes)
        {
            var path = NormalizeRepositoryPath(change.Path);
            var operation = change.Kind switch
            {
                GitCommitChangeKind.Add => "create",
                GitCommitChangeKind.Edit => "update",
                GitCommitChangeKind.Delete => "delete",
                _ => throw new ArgumentOutOfRangeException(nameof(changes), "The file change kind is not supported.")
            };
            if (change.Kind is GitCommitChangeKind.Add or GitCommitChangeKind.Edit && change.Content is null)
            {
                throw new ArgumentException("Added and edited files require content.", nameof(changes));
            }

            var sha = change.Kind is GitCommitChangeKind.Edit or GitCommitChangeKind.Delete
                ? await GetFileShaAsync(
                    connection,
                    repositoryId,
                    normalizedBranch,
                    path,
                    transientAccessToken,
                    cancellationToken)
                : null;
            var content = change.Content is null
                ? null
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(change.Content));
            fileChanges.Add(new GiteaChangeFilePayload(operation, path, content, sha));
        }

        var response = await SendJsonAsync<GiteaFilesResponse>(
            connection,
            repositoryId,
            HttpMethod.Post,
            "contents",
            null,
            transientAccessToken,
            new { branch = normalizedBranch, message, files = fileChanges },
            cancellationToken);
        return response?.Commit?.Sha
            ?? throw new InvalidOperationException("Gitea did not return the commit created for a publication.");
    }

    private async Task<string> GetFileShaAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string path,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var payload = await SendJsonAsync<GiteaFilePayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"contents/{path}",
            $"ref={Uri.EscapeDataString(branchName)}",
            transientAccessToken,
            null,
            cancellationToken,
            missingIsNull: true);
        if (payload is null
            || !string.Equals(payload.Type, "file", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(payload.Sha))
        {
            throw new InvalidOperationException("Gitea did not return the existing file SHA required for a change.");
        }

        return payload.Sha;
    }

    public async Task<GitPullRequestInfo?> GetOpenPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string sourceBranch,
        string targetBranch,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var (owner, _) = ParseRepositoryId(connection, repositoryId);
        var query = string.Join('&',
            "state=open",
            $"head={Uri.EscapeDataString(string.Concat(owner, ":", NormalizeBranch(sourceBranch)))}",
            $"base={Uri.EscapeDataString(NormalizeBranch(targetBranch))}");
        var payload = await SendJsonAsync<IReadOnlyList<GiteaPullRequestPayload>>(
            connection,
            repositoryId,
            HttpMethod.Get,
            "pulls",
            query,
            transientAccessToken,
            null,
            cancellationToken);
        var pullRequest = payload is { Count: > 0 } ? payload[0] : null;
        return pullRequest is null ? null : ToPullRequest(pullRequest);
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
        var body = new
        {
            head = NormalizeBranch(sourceBranch),
            @base = NormalizeBranch(targetBranch),
            title,
            body = description,
            reviewers = reviewers.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray()
        };
        var method = existingPullRequestId is null ? HttpMethod.Post : HttpMethod.Patch;
        var path = existingPullRequestId is null ? "pulls" : $"pulls/{Uri.EscapeDataString(existingPullRequestId)}";
        var payload = await SendJsonAsync<GiteaPullRequestPayload>(
            connection,
            repositoryId,
            method,
            path,
            null,
            transientAccessToken,
            body,
            cancellationToken);
        return payload is null
            ? throw new InvalidOperationException("Gitea did not return the pull request created for a publication.")
            : ToPullRequest(payload);
    }

    public async Task<GitPullRequestInfo> GetPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string pullRequestId,
        string? transientAccessToken,
        CancellationToken cancellationToken)
    {
        var payload = await SendJsonAsync<GiteaPullRequestPayload>(
            connection,
            repositoryId,
            HttpMethod.Get,
            $"pulls/{Uri.EscapeDataString(pullRequestId)}",
            null,
            transientAccessToken,
            null,
            cancellationToken);
        return payload is null
            ? throw new InvalidOperationException("Gitea returned an empty pull request response.")
            : ToPullRequest(payload);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        GitConnection connection,
        string repositoryId,
        HttpMethod method,
        string path,
        string? query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken,
        bool preserveEscapedBranchName = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.ProviderKind != Key
            || connection.AuthenticationMode != GitConnectionAuthenticationMode.DevelopmentToken)
        {
            throw new ArgumentException("The connection is not a development Gitea connection.", nameof(connection));
        }

        var token = transientAccessToken ?? configuration[TokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("The development Gitea token is not configured.");
        }

        var endpoint = configuration[EndpointConfigurationKey] ?? "http://localhost:3000";
        if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/", UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{EndpointConfigurationKey} must be an HTTP endpoint.");
        }

        var (owner, name) = ParseRepositoryId(connection, repositoryId);
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var escapedPath = string.Join('/', pathSegments.Select((segment, index) =>
            preserveEscapedBranchName && index == 1 ? segment : Uri.EscapeDataString(segment)));
        var requestUri = new Uri(endpointUri,
            $"api/v1/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/{escapedPath}{(query is null ? string.Empty : string.Concat("?", query))}");
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
        string? query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken,
        bool missingIsNull = false,
        bool preserveEscapedBranchName = false)
    {
        using var request = await CreateRequestAsync(
            connection,
            repositoryId,
            method,
            path,
            query,
            transientAccessToken,
            body,
            cancellationToken,
            preserveEscapedBranchName);
        using var response = await httpClientFactory.CreateClient(nameof(GiteaProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (missingIsNull && response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task SendAsync(
        GitConnection connection,
        string repositoryId,
        HttpMethod method,
        string path,
        string? query,
        string? transientAccessToken,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            connection,
            repositoryId,
            method,
            path,
            query,
            transientAccessToken,
            body,
            cancellationToken);
        using var response = await httpClientFactory.CreateClient(nameof(GiteaProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static GitPullRequestInfo ToPullRequest(GiteaPullRequestPayload payload) => new(
        payload.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        payload.HtmlUrl,
        payload.Head.Ref,
        payload.Base.Ref,
        payload.Merged
            ? GitPullRequestState.Merged
            : string.Equals(payload.State, "open", StringComparison.OrdinalIgnoreCase)
                ? GitPullRequestState.Open
                : GitPullRequestState.Closed,
        payload.MergeCommitSha);

    private static (string Owner, string Name) ParseRepositoryId(GitConnection connection, string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        var segments = repositoryId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length switch
        {
            1 => (connection.Organization, segments[0]),
            2 => (segments[0], segments[1]),
            _ => throw new ArgumentException("A Gitea repository ID must be 'owner/name'.", nameof(repositoryId))
        };
    }

    private static string NormalizeBranch(string branchName) =>
        branchName.StartsWith("refs/heads/", StringComparison.Ordinal) ? branchName["refs/heads/".Length..] : branchName;

    private static string NormalizeRepositoryPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A safe repository-relative path is required.", nameof(path));
        }

        return normalized;
    }

    private sealed record GiteaBranchPayload([property: JsonPropertyName("commit")] GiteaBranchCommitPayload? Commit);

    private sealed record GiteaBranchCommitPayload(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record GiteaTreePayload([property: JsonPropertyName("tree")] IReadOnlyList<GiteaTreeItemPayload> Tree);

    private sealed record GiteaTreeItemPayload(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type);

    private sealed record GiteaFilePayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("encoding")] string Encoding,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("sha")] string? Sha);

    private sealed record GiteaFilesResponse([property: JsonPropertyName("commit")] GiteaFileCommitPayload? Commit);

    private sealed record GiteaFileCommitPayload([property: JsonPropertyName("sha")] string? Sha);

    private sealed record GiteaChangeFilePayload(
        [property: JsonPropertyName("operation")] string Operation,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Content,
        [property: JsonPropertyName("sha"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sha);

    private sealed record GiteaCommitPayload(
        [property: JsonPropertyName("sha")] string? Sha,
        [property: JsonPropertyName("tree")] GiteaTreeReferencePayload? Tree);

    private sealed record GiteaTreeReferencePayload([property: JsonPropertyName("sha")] string Sha);

    private sealed record GiteaPullRequestPayload(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("merged")] bool Merged,
        [property: JsonPropertyName("merge_commit_sha")] string? MergeCommitSha,
        [property: JsonPropertyName("head")] GiteaPullRequestBranchPayload Head,
        [property: JsonPropertyName("base")] GiteaPullRequestBranchPayload Base);

    private sealed record GiteaPullRequestBranchPayload([property: JsonPropertyName("ref")] string Ref);
}
