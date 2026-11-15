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
    public void RepreparationKeepsGeneratedFilesAndDropsPriorDeletionRows()
    {
        var destinationId = Guid.NewGuid();
        var publication = Publication.Start(
            new PublicationId(Guid.NewGuid()),
            new OrganizationId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new RevisionId(Guid.NewGuid()),
            1,
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            DateTimeOffset.UtcNow,
            null,
            [CreateTarget("Contoso/Shop", destinationId: destinationId, files:
            [
                new PublicationTargetFileInput(destinationId, "infra/main.bicep", new string('b', 64), PublicationFileAction.Add, null)
            ])]);
        Assert.False(publication.IsError);

        var target = Assert.Single(publication.Value.Targets);
        Assert.Equal(destinationId, Assert.Single(target.Destinations).DestinationId);
        var prepared = publication.Value.MarkTargetPrepared(
            target.Id,
            "main",
            new string('a', 40),
            [
                new PublicationTargetFileInput(destinationId, "infra/main.bicep", new string('b', 64), PublicationFileAction.Add, null),
                new PublicationTargetFileInput(destinationId, "obsolete/old.txt", null, PublicationFileAction.Delete, new string('c', 64)),
                new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", new string('d', 64), PublicationFileAction.Add, null)
            ],
            requiresConfirmation: false,
            DateTimeOffset.UtcNow);
        Assert.False(prepared.IsError);
        Assert.False(publication.Value.BeginWriting().IsError);

        Assert.False(publication.Value.ResetForRepreparation().IsError);

        Assert.Equal(PublicationTargetState.Preparing, target.State);
        Assert.Contains(target.Files, file => file.RepositoryPath == "infra/main.bicep");
        Assert.Contains(target.Files, file => file.RepositoryPath == ".ifs/manifest.json");
        Assert.DoesNotContain(target.Files, file => file.Action == PublicationFileAction.Delete);
    }

    [Fact]
    public void RecoveredCommitAdoptsTheManifestHashThatWasActuallyCommitted()
    {
        var publication = CreatePublication();
        var target = Assert.Single(publication.Targets);
        var destinationId = Assert.Single(target.Destinations).DestinationId;
        var currentManifestSha = new string('d', 64);
        var priorManifestSha = new string('e', 64);
        Assert.False(publication.MarkTargetPrepared(
            target.Id,
            "main",
            new string('a', 40),
            [
                new PublicationTargetFileInput(destinationId, "infra/main.bicep", new string('b', 64), PublicationFileAction.Update, new string('c', 64)),
                new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", currentManifestSha, PublicationFileAction.Update, new string('f', 64))
            ],
            requiresConfirmation: true,
            DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.ConfirmOwnership(target.Id, DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.BeginWriting().IsError);

        var adopted = publication.RecordRecoveredCommit(
            target.Id,
            new string('c', 40),
            [new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", priorManifestSha, PublicationFileAction.Update, new string('f', 64))]);

        Assert.False(adopted.IsError);
        Assert.Equal(priorManifestSha, Assert.Single(target.Files, file => file.RepositoryPath == ".ifs/manifest.json").Sha256);
        Assert.Equal(new string('b', 64), Assert.Single(target.Files, file => file.RepositoryPath == "infra/main.bicep").Sha256);
        Assert.Equal(new string('c', 40), target.CommitSha);
    }

    [Fact]
    public void RecoveredCommitWithChangedUnchangedManifestHashIsRejectedWithoutMutation()
    {
        var publication = CreatePublication();
        var target = Assert.Single(publication.Targets);
        var destinationId = Assert.Single(target.Destinations).DestinationId;
        var manifestSha = new string('d', 64);
        Assert.False(publication.MarkTargetPrepared(
            target.Id,
            "main",
            new string('a', 40),
            [new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", manifestSha, PublicationFileAction.Unchanged, manifestSha)],
            requiresConfirmation: false,
            DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.BeginWriting().IsError);

        var recovered = publication.RecordRecoveredCommit(
            target.Id,
            new string('c', 40),
            [new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", new string('e', 64), PublicationFileAction.Unchanged, manifestSha)]);

        Assert.True(recovered.IsError);
        Assert.Null(target.CommitSha);
        Assert.Equal(manifestSha, Assert.Single(target.Files).Sha256);
    }

    [Fact]
    public void RecoveredCommitWithDifferentKnownShaDoesNotChangePreparedManifest()
    {
        var publication = CreatePublication();
        var target = Assert.Single(publication.Targets);
        var destinationId = Assert.Single(target.Destinations).DestinationId;
        var currentManifestSha = new string('d', 64);
        var priorManifestSha = new string('e', 64);
        Assert.False(publication.MarkTargetPrepared(
            target.Id,
            "main",
            new string('a', 40),
            [new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", currentManifestSha, PublicationFileAction.Add, null)],
            requiresConfirmation: false,
            DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.BeginWriting().IsError);
        var knownCommitSha = new string('b', 40);
        Assert.False(publication.RecordCommit(target.Id, knownCommitSha).IsError);

        var recovered = publication.RecordRecoveredCommit(
            target.Id,
            new string('c', 40),
            [new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", priorManifestSha, PublicationFileAction.Add, null)]);

        Assert.True(recovered.IsError);
        Assert.Equal(knownCommitSha, target.CommitSha);
        Assert.Equal(currentManifestSha, Assert.Single(target.Files).Sha256);
    }

    [Fact]
    public void RecoveredCommitRejectsInvalidManifestReferencesWithoutMutation()
    {
        var publication = CreatePublication();
        var target = Assert.Single(publication.Targets);
        var destinationId = Assert.Single(target.Destinations).DestinationId;
        var currentManifestSha = new string('d', 64);
        var existingManifestSha = new string('f', 64);
        var recoveredManifest = new PublicationTargetFileInput(
            destinationId,
            ".ifs/manifest.json",
            new string('e', 64),
            PublicationFileAction.Update,
            existingManifestSha);
        Assert.False(publication.MarkTargetPrepared(
            target.Id,
            "main",
            new string('a', 40),
            [
                new PublicationTargetFileInput(destinationId, "infra/main.bicep", new string('b', 64), PublicationFileAction.Update, new string('c', 64)),
                new PublicationTargetFileInput(destinationId, ".ifs/manifest.json", currentManifestSha, PublicationFileAction.Update, existingManifestSha)
            ],
            requiresConfirmation: true,
            DateTimeOffset.UtcNow).IsError);

        Assert.True(publication.RecordRecoveredCommit(target.Id, new string('c', 40), [recoveredManifest]).IsError);
        Assert.False(publication.ConfirmOwnership(target.Id, DateTimeOffset.UtcNow).IsError);
        Assert.False(publication.BeginWriting().IsError);

        var invalidManifests = new IReadOnlyCollection<PublicationTargetFileInput>[]
        {
            [],
            [recoveredManifest with { Action = PublicationFileAction.Add, ExistingSha256 = null }],
            [recoveredManifest with { ExistingSha256 = new string('g', 64) }],
            [recoveredManifest with { Sha256 = "invalid" }],
            [recoveredManifest, recoveredManifest]
        };
        foreach (var invalidManifest in invalidManifests)
        {
            Assert.True(publication.RecordRecoveredCommit(target.Id, new string('c', 40), invalidManifest).IsError);
            Assert.Null(target.CommitSha);
            Assert.Equal(currentManifestSha, Assert.Single(target.Files, file => file.RepositoryPath == ".ifs/manifest.json").Sha256);
            Assert.Equal(new string('b', 64), Assert.Single(target.Files, file => file.RepositoryPath == "infra/main.bicep").Sha256);
        }
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

    private static PublicationTargetInput CreateTarget(
        string repositoryId,
        GitConnectionId? connectionId = null,
        IReadOnlyCollection<PublicationTargetFileInput>? files = null,
        Guid? destinationId = null) => new(
            connectionId ?? new GitConnectionId(Guid.NewGuid()),
            repositoryId,
            "shop",
            "main",
            "ifs/shop/revision-1",
            [new PublicationTargetDestinationInput(destinationId ?? Guid.NewGuid(), "")],
            files ?? []);
}
