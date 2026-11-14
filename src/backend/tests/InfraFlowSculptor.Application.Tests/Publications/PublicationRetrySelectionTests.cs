using InfraFlowSculptor.Application.Publications;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;

namespace InfraFlowSculptor.Application.Tests.Publications;

public sealed class PublicationRetrySelectionTests
{
    [Fact]
    public void AllowsAllDestinationsOfOneFailedRepositoryAfterPartialPublication()
    {
        var (publication, _, failedDestinationIds) = CreatePartiallyPublished();

        Assert.True(PublicationRetrySelection.IsValid(publication, failedDestinationIds));
    }

    [Fact]
    public void RejectsPartialOrAlreadyPublishedRepositoryDestinations()
    {
        var (publication, completedDestinationId, failedDestinationIds) = CreatePartiallyPublished();
        var firstFailedDestinationId = failedDestinationIds.First();

        Assert.False(PublicationRetrySelection.IsValid(publication, [firstFailedDestinationId]));
        Assert.False(PublicationRetrySelection.IsValid(publication, [completedDestinationId]));
        Assert.False(PublicationRetrySelection.IsValid(
            publication,
            [.. failedDestinationIds, completedDestinationId]));
        Assert.False(PublicationRetrySelection.IsValid(
            publication,
            [firstFailedDestinationId, firstFailedDestinationId]));
    }

    private static (PublicationAggregate Publication, Guid CompletedDestinationId, IReadOnlyCollection<Guid> FailedDestinationIds)
        CreatePartiallyPublished()
    {
        var completedDestinationId = Guid.NewGuid();
        var failedDestinationIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var completedTarget = CreateTarget("contoso/completed", [completedDestinationId]);
        var failedTarget = CreateTarget("contoso/failed", failedDestinationIds);
        var result = PublicationAggregate.Start(
            new PublicationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new RevisionId(Guid.NewGuid()),
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            DateTimeOffset.UtcNow,
            null,
            [completedTarget, failedTarget]);

        Assert.False(result.IsError);
        var publication = result.Value;
        foreach (var target in publication.Targets)
        {
            Assert.False(publication.MarkTargetPrepared(
                target.Id,
                "main",
                new string('a', 40),
                [],
                requiresConfirmation: false,
                DateTimeOffset.UtcNow).IsError);
        }

        Assert.False(publication.BeginWriting().IsError);
        var firstTarget = publication.Targets.Single(target => target.RepositoryName == "completed");
        Assert.False(publication.RecordPullRequest(
            firstTarget.Id,
            new string('c', 40),
            "1",
            "https://git.example/contoso/completed/pulls/1",
            DateTimeOffset.UtcNow).IsError);
        var secondTarget = publication.Targets.Single(target => target.RepositoryName == "failed");
        Assert.False(publication.MarkTargetFailed(
            secondTarget.Id,
            PublicationValidationCodes.ProviderFailure,
            DateTimeOffset.UtcNow).IsError);

        Assert.Equal(PublicationState.PartiallyPublished, publication.State);
        return (publication, completedDestinationId, failedDestinationIds);
    }

    private static PublicationTargetInput CreateTarget(string repositoryId, IReadOnlyCollection<Guid> destinationIds) => new(
        new GitConnectionId(Guid.NewGuid()),
        repositoryId,
        repositoryId.Split('/')[1],
        "main",
        "ifs/shop/revision-1",
        destinationIds.Select(id => new PublicationTargetDestinationInput(id, "")).ToArray(),
        []);
}
