namespace InfraFlowSculptor.Application.GitConnections;

public sealed record GitPullRequestInfo(
    string Id,
    string Url,
    string SourceBranch,
    string TargetBranch,
    GitPullRequestState State,
    string? MergeCommitSha);
