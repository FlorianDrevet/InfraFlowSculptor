using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.Common.Extensibility;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public interface IGitProvider : IKeyed<GitProviderKind>
{
    string DisplayName { get; }

    Task<IReadOnlyCollection<GitRepositoryInfo>> GetRepositoriesAsync(
        GitConnection connection,
        string? transientAccessToken,
        CancellationToken cancellationToken);

    Task<GitBranchInfo?> GetBranchAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support branch reads.");

    Task<IReadOnlyCollection<string>> GetTreePathsAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support repository tree reads.");

    Task<string?> GetFileContentAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string path,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support file reads.");

    Task CreateBranchAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string baseCommitSha,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support branch creation.");

    Task<string> CreateCommitAsync(
        GitConnection connection,
        string repositoryId,
        string branchName,
        string expectedBaseSha,
        IReadOnlyCollection<GitCommitChange> changes,
        string message,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support commits.");

    Task<GitPullRequestInfo?> GetOpenPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string sourceBranch,
        string targetBranch,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support pull request reads.");

    Task<GitPullRequestInfo> CreateOrUpdatePullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string? existingPullRequestId,
        string sourceBranch,
        string targetBranch,
        string title,
        string description,
        IReadOnlyCollection<string> reviewers,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support pull request writes.");

    Task<GitPullRequestInfo> GetPullRequestAsync(
        GitConnection connection,
        string repositoryId,
        string pullRequestId,
        string? transientAccessToken,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This Git provider does not support pull request reads.");
}
