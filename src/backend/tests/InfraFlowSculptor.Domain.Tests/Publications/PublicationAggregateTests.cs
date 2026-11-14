using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Publications;

public sealed class PublicationAggregateTests
{
    [Fact]
    public void PublicationRequiresOwnershipConfirmationAndRecordsPullRequestIdempotently()
    {
        var publication = CreatePublication();
        var target = Assert.Single(publication.Targets);
        var targetId = target.Id;
        var destinationId = Assert.Single(target.Destinations).DestinationId;
        var prepared = publication.MarkTargetPrepared(
            targetId,
            "main",
            new string('a', 40),
            [new PublicationTargetFileInput(destinationId, "infra/main.bicep", new string('b', 64), PublicationFileAction.Add, null)],
            requiresConfirmation: true,
            DateTimeOffset.UtcNow);

        Assert.False(prepared.IsError);
        Assert.Equal(PublicationState.NeedsConfirmation, publication.State);
        Assert.True(publication.BeginWriting().IsError);

        Assert.False(publication.ConfirmOwnership(targetId, DateTimeOffset.UtcNow).IsError);
        Assert.Equal(PublicationState.Prepared, publication.State);
        Assert.False(publication.BeginWriting().IsError);

        var recorded = publication.RecordPullRequest(
            targetId,
            new string('c', 40),
            "42",
            "https://git.example/contoso/shop/pulls/42",
            DateTimeOffset.UtcNow);
        var replayed = publication.RecordPullRequest(
            targetId,
            new string('c', 40),
            "42",
            "https://git.example/contoso/shop/pulls/42",
            DateTimeOffset.UtcNow);

        Assert.False(recorded.IsError);
        Assert.False(replayed.IsError);
        Assert.Equal(PublicationState.Completed, publication.State);
        Assert.Equal("42", Assert.Single(publication.Targets).PullRequestId);
        Assert.Equal(new string('c', 40), Assert.Single(publication.Targets).CommitSha);
        Assert.Single(Assert.Single(publication.Targets).Files);
    }

    [Fact]
    public void PublicationRejectsDuplicateRepositoryTargets()
    {
        var connectionId = new GitConnectionId(Guid.NewGuid());
        var first = CreateTarget("Contoso/Shop", connectionId);
        var duplicate = CreateTarget("contoso/shop", connectionId);

        var result = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new RevisionId(Guid.NewGuid()),
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            DateTimeOffset.UtcNow,
            null,
            [first, duplicate]);

        Assert.True(result.IsError);
        Assert.Equal(PublicationValidationCodes.InvalidTarget, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void MergedPullRequestRequiresAndStoresMergeCommit()
    {
        var publication = CreatePublication();
        var targetId = Assert.Single(publication.Targets).Id;
        Assert.False(publication.MarkTargetPrepared(targetId, "main", new string('a', 40), [], false, DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.BeginWriting().IsError);
        Assert.False(publication.RecordPullRequest(
            targetId,
            new string('c', 40),
            "42",
            "https://git.example/contoso/shop/pulls/42",
            DateTimeOffset.UtcNow).IsError);

        Assert.True(publication.UpdatePullRequestState(
            targetId,
            PublicationPullRequestState.Merged,
            null,
            DateTimeOffset.UtcNow).IsError);

        var mergeSha = new string('d', 40);
        Assert.False(publication.UpdatePullRequestState(
            targetId,
            PublicationPullRequestState.Merged,
            mergeSha,
            DateTimeOffset.UtcNow).IsError);
        Assert.Equal(mergeSha, Assert.Single(publication.Targets).MergeCommitSha);
    }

    private static Publication CreatePublication(bool requiresConfirmation = true)
    {
        var result = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new RevisionId(Guid.NewGuid()),
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            DateTimeOffset.UtcNow,
            null,
            [CreateTarget("Contoso/Shop")]);

        Assert.False(result.IsError);
        return result.Value;
    }

    private static PublicationTargetInput CreateTarget(string repositoryId, GitConnectionId? connectionId = null) => new(
        connectionId ?? new GitConnectionId(Guid.NewGuid()),
        repositoryId,
        "shop",
        "main",
        "ifs/shop/revision-1",
        [new PublicationTargetDestinationInput(Guid.NewGuid(), "")],
        []);
}
