using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;

namespace InfraFlowSculptor.Worker.Jobs;

internal static class PublicationCommitRecovery
{
    public static PublicationCommitRecoveryResult? FindCommit(
        GitBranchInfo? sourceBranch,
        PublicationId currentPublicationId,
        GitConnectionId gitConnectionId,
        string providerRepositoryId,
        string sourceBranchName,
        string baseCommitSha,
        string? currentKnownCommitSha,
        IReadOnlyCollection<PublicationCommitFileReference> currentFiles,
        IReadOnlyCollection<PublicationCommitReference> priorCommits)
    {
        if (sourceBranch is null)
        {
            return null;
        }

        if (HasOperationTag(sourceBranch, currentPublicationId))
        {
            return currentKnownCommitSha is null
                || string.Equals(currentKnownCommitSha, sourceBranch.CommitSha, StringComparison.OrdinalIgnoreCase)
                    ? new PublicationCommitRecoveryResult(sourceBranch.CommitSha, null)
                    : null;
        }

        var priorCommit = priorCommits.FirstOrDefault(candidate =>
            candidate.GitConnectionId == gitConnectionId
            && string.Equals(candidate.ProviderRepositoryId, providerRepositoryId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.SourceBranch, sourceBranchName, StringComparison.Ordinal)
            && string.Equals(candidate.BaseCommitSha, baseCommitSha, StringComparison.OrdinalIgnoreCase)
            && candidate.CommitSha is not null
            && (currentKnownCommitSha is null
                || string.Equals(candidate.CommitSha, currentKnownCommitSha, StringComparison.OrdinalIgnoreCase))
            && string.Equals(candidate.CommitSha, sourceBranch.CommitSha, StringComparison.OrdinalIgnoreCase)
            && HasSameGeneratedFiles(currentFiles, candidate.Files)
            && HasSameManifestFiles(currentFiles, candidate.Files)
            && HasOperationTag(sourceBranch, candidate.PublicationId));
        return priorCommit is null
            ? null
            : new PublicationCommitRecoveryResult(priorCommit.CommitSha!, priorCommit.Files);
    }

    public static bool HasOperationTag(GitBranchInfo branch, PublicationId publicationId) =>
        branch.CommitMessage.Contains(
            string.Concat("Publication-Id: ", publicationId.Value.ToString("D")),
            StringComparison.Ordinal);

    private static bool HasSameGeneratedFiles(
        IReadOnlyCollection<PublicationCommitFileReference> currentFiles,
        IReadOnlyCollection<PublicationCommitFileReference> priorFiles)
    {
        static PublicationCommitFileReference[] GeneratedFiles(
            IReadOnlyCollection<PublicationCommitFileReference> files) => files
            .Where(file => !PublicationManifestPaths.IsManifestFilePath(file.RepositoryPath))
            .OrderBy(file => file.RepositoryPath, StringComparer.Ordinal)
            .ThenBy(file => file.DestinationId)
            .ToArray();

        return GeneratedFiles(currentFiles).SequenceEqual(GeneratedFiles(priorFiles));
    }

    private static bool HasSameManifestFiles(
        IReadOnlyCollection<PublicationCommitFileReference> currentFiles,
        IReadOnlyCollection<PublicationCommitFileReference> priorFiles)
    {
        static PublicationCommitFileReference[] ManifestFiles(
            IReadOnlyCollection<PublicationCommitFileReference> files) => files
            .Where(file => PublicationManifestPaths.IsManifestFilePath(file.RepositoryPath))
            .OrderBy(file => file.DestinationId)
            .ThenBy(file => file.RepositoryPath, StringComparer.Ordinal)
            .ToArray();

        var currentManifests = ManifestFiles(currentFiles);
        var priorManifests = ManifestFiles(priorFiles);
        return currentManifests.Length > 0
            && currentManifests.Length == priorManifests.Length
            && currentManifests.Zip(priorManifests).All(pair =>
                pair.First.DestinationId == pair.Second.DestinationId
                && string.Equals(pair.First.RepositoryPath, pair.Second.RepositoryPath, StringComparison.Ordinal)
                && pair.First.Sha256 is not null
                && pair.Second.Sha256 is not null
                && pair.First.Action == pair.Second.Action
                && string.Equals(pair.First.ExistingSha256, pair.Second.ExistingSha256, StringComparison.Ordinal));
    }
}

internal sealed record PublicationCommitRecoveryResult(
    string CommitSha,
    IReadOnlyCollection<PublicationCommitFileReference>? ReusedFiles);

internal sealed record PublicationCommitReference(
    PublicationId PublicationId,
    GitConnectionId GitConnectionId,
    string ProviderRepositoryId,
    string SourceBranch,
    string BaseCommitSha,
    string? CommitSha,
    IReadOnlyList<PublicationCommitFileReference> Files);

internal sealed record PublicationCommitFileReference(
    Guid DestinationId,
    string RepositoryPath,
    string? Sha256,
    PublicationFileAction Action,
    string? ExistingSha256);
