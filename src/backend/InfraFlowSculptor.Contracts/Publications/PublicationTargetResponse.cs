namespace InfraFlowSculptor.Contracts.Publications;

public sealed record PublicationTargetResponse(
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
    IReadOnlyCollection<PublicationTargetDestinationResponse> Destinations,
    IReadOnlyCollection<PublicationTargetFileResponse> Files);
