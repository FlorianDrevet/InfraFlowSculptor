using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Infrastructure.Tests.Publications;

public sealed class PublicationCommitRecoveryTests
{
    [Fact]
    public void FindCommitRecoversCommitWhenGeneratedFilesMatchDespiteRetryManifestTimestamp()
    {
        var previousPublicationId = new PublicationId(Guid.NewGuid());
        var destinationId = Guid.NewGuid();
        var branch = new GitBranchInfo(
            "ifs/shop/revision-1",
            new string('c', 40),
            $"IFS révision 1\n\nPublication-Id: {previousPublicationId.Value:D}");
        var priorCommit = new PublicationCommitReference(
            previousPublicationId,
            new GitConnectionId(Guid.NewGuid()),
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            branch.CommitSha,
            [
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/infra/main.bicep",
                    new string('a', 64),
                    PublicationFileAction.Update,
                    new string('b', 64)),
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/.ifs/manifest.json",
                    new string('c', 64),
                    PublicationFileAction.Update,
                    new string('d', 64))
            ]);
        var currentFiles = new[]
        {
            priorCommit.Files[0],
            priorCommit.Files[1] with { Sha256 = new string('e', 64) }
        };
        var currentPublicationId = new PublicationId(Guid.NewGuid());

        var recovered = PublicationCommitRecovery.FindCommit(
            branch,
            currentPublicationId,
            priorCommit.GitConnectionId,
            priorCommit.ProviderRepositoryId,
            priorCommit.SourceBranch,
            priorCommit.BaseCommitSha,
            null,
            currentFiles,
            [priorCommit]);

        Assert.Equal(branch.CommitSha, recovered?.CommitSha);
        Assert.Equal(
            new string('c', 64),
            Assert.Single(recovered!.ReusedFiles!, file => PublicationManifestPaths.IsManifestFilePath(file.RepositoryPath)).Sha256);

        PublicationCommitRecoveryResult? Recover(PublicationCommitReference candidate) => PublicationCommitRecovery.FindCommit(
            branch,
            currentPublicationId,
            candidate.GitConnectionId,
            candidate.ProviderRepositoryId,
            candidate.SourceBranch,
            candidate.BaseCommitSha,
            null,
            currentFiles,
            [candidate]);

        Assert.Null(Recover(priorCommit with
        {
            Files = [priorCommit.Files[0], priorCommit.Files[1] with { Action = PublicationFileAction.Add }]
        }));
        Assert.Null(Recover(priorCommit with
        {
            Files = [priorCommit.Files[0], priorCommit.Files[1] with { ExistingSha256 = new string('f', 64) }]
        }));
        Assert.Null(Recover(priorCommit with { Files = [priorCommit.Files[0]] }));
        Assert.Null(Recover(priorCommit with
        {
            Files = [priorCommit.Files[0], priorCommit.Files[1] with { Sha256 = null }]
        }));

        var rejectedKnownSha = PublicationCommitRecovery.FindCommit(
            branch,
            currentPublicationId,
            priorCommit.GitConnectionId,
            priorCommit.ProviderRepositoryId,
            priorCommit.SourceBranch,
            priorCommit.BaseCommitSha,
            new string('f', 40),
            currentFiles,
            [priorCommit]);
        Assert.Null(rejectedKnownSha);
    }

    [Fact]
    public void FindCommitRecoversCurrentOperationTagOnlyWhenKnownShaMatches()
    {
        var publicationId = new PublicationId(Guid.NewGuid());
        var commitSha = new string('c', 40);
        var branch = new GitBranchInfo(
            "ifs/shop/revision-1",
            commitSha,
            $"IFS révision 1\n\nPublication-Id: {publicationId.Value:D}");
        var connectionId = new GitConnectionId(Guid.NewGuid());

        var recoveredWithoutKnownSha = PublicationCommitRecovery.FindCommit(
            branch,
            publicationId,
            connectionId,
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            null,
            [],
            []);
        var recoveredWithKnownSha = PublicationCommitRecovery.FindCommit(
            branch,
            publicationId,
            connectionId,
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            commitSha,
            [],
            []);
        var rejectedWithChangedKnownSha = PublicationCommitRecovery.FindCommit(
            branch,
            publicationId,
            connectionId,
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            new string('d', 40),
            [],
            []);

        Assert.Equal(commitSha, recoveredWithoutKnownSha?.CommitSha);
        Assert.Equal(commitSha, recoveredWithKnownSha?.CommitSha);
        Assert.Null(rejectedWithChangedKnownSha);
        Assert.Null(recoveredWithKnownSha?.ReusedFiles);
    }

    [Fact]
    public void FindCommitShaRejectsPreviousCommitWhenPreparedBaseHasAdvanced()
    {
        var previousPublicationId = new PublicationId(Guid.NewGuid());
        var destinationId = Guid.NewGuid();
        var branch = new GitBranchInfo(
            "ifs/shop/revision-1",
            new string('c', 40),
            $"IFS révision 1\n\nPublication-Id: {previousPublicationId.Value:D}");
        var priorCommit = new PublicationCommitReference(
            previousPublicationId,
            new GitConnectionId(Guid.NewGuid()),
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            branch.CommitSha,
            [
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/infra/main.bicep",
                    new string('a', 64),
                    PublicationFileAction.Update,
                    new string('b', 64)),
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/.ifs/manifest.json",
                    new string('c', 64),
                    PublicationFileAction.Update,
                    new string('d', 64))
            ]);
        var recovered = PublicationCommitRecovery.FindCommit(
            branch,
            new PublicationId(Guid.NewGuid()),
            priorCommit.GitConnectionId,
            priorCommit.ProviderRepositoryId,
            priorCommit.SourceBranch,
            new string('d', 40),
            null,
            priorCommit.Files,
            [priorCommit]);

        Assert.Null(recovered);
    }

    [Fact]
    public void FindCommitShaRejectsPreviousCommitWhenPreparedFilesHaveChanged()
    {
        var previousPublicationId = new PublicationId(Guid.NewGuid());
        var destinationId = Guid.NewGuid();
        var branch = new GitBranchInfo(
            "ifs/shop/revision-1",
            new string('c', 40),
            $"IFS révision 1\n\nPublication-Id: {previousPublicationId.Value:D}");
        var priorCommit = new PublicationCommitReference(
            previousPublicationId,
            new GitConnectionId(Guid.NewGuid()),
            "contoso/shop",
            branch.Name,
            new string('b', 40),
            branch.CommitSha,
            [
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/infra/main.bicep",
                    new string('a', 64),
                    PublicationFileAction.Update,
                    new string('b', 64)),
                new PublicationCommitFileReference(
                    destinationId,
                    "orders/.ifs/manifest.json",
                    new string('c', 64),
                    PublicationFileAction.Update,
                    new string('d', 64))
            ]);

        var recovered = PublicationCommitRecovery.FindCommit(
            branch,
            new PublicationId(Guid.NewGuid()),
            priorCommit.GitConnectionId,
            priorCommit.ProviderRepositoryId,
            priorCommit.SourceBranch,
            priorCommit.BaseCommitSha,
            null,
            [priorCommit.Files[0] with { Sha256 = new string('d', 64) }, priorCommit.Files[1]],
            [priorCommit]);

        Assert.Null(recovered);
    }
}
