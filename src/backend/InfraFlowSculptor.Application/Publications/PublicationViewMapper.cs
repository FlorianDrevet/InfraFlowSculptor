using InfraFlowSculptor.Domain.PublicationAggregate;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public static class PublicationViewMapper
{
    public static PublicationView Map(Publication publication) => new(
        publication.Id.Value,
        publication.ProjectId.Value,
        publication.RevisionId.Value,
        publication.RevisionNumber,
        publication.Message,
        publication.State.ToString(),
        publication.RequestedAt,
        publication.PreparedAt,
        publication.CompletedAt,
        publication.Targets.Select(target => new PublicationTargetView(
            target.Id,
            target.GitConnectionId.Value,
            target.ProviderRepositoryId,
            target.RepositoryName,
            target.TargetBranch,
            target.SourceBranch,
            target.State.ToString(),
            target.RequiresConfirmation,
            target.OwnershipConfirmed,
            target.FailureCode,
            target.CommitSha,
            target.PullRequestId,
            target.PullRequestUrl,
            target.PullRequestState.ToString(),
            target.MergeCommitSha,
            target.Destinations.Select(destination => new PublicationTargetDestinationView(
                destination.DestinationId,
                destination.BasePath)).ToArray(),
            target.Files.Select(file => new PublicationTargetFileView(
                file.DestinationId,
                file.RepositoryPath,
                file.Action.ToString(),
                file.Sha256,
                file.ExistingSha256)).ToArray())).ToArray());
}
