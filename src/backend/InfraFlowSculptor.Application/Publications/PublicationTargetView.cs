namespace InfraFlowSculptor.Application.Publications;

public sealed record PublicationTargetView(
    Guid Id,
    Guid GitConnectionId,
    string ProviderRepositoryId,
    string RepositoryName,
    string TargetBranch,
    string SourceBranch,
    string State,
    bool RequiresConfirmation,
    bool OwnershipConfirmed,
    string? FailureCode,
    string? CommitSha,
    string? PullRequestId,
    string? PullRequestUrl,
    string PullRequestState,
    string? MergeCommitSha,
    IReadOnlyCollection<PublicationTargetDestinationView> Destinations,
    IReadOnlyCollection<PublicationTargetFileView> Files);
