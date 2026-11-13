using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Generation;

public interface IRevisionBlobStore
{
    Task EnsureFileAsync(string sha256, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<Stream> OpenFileAsync(string sha256, CancellationToken cancellationToken);

    Task<Stream> GetOrCreateArchiveAsync(
        Guid revisionId,
        IReadOnlyCollection<RevisionFile> files,
        CancellationToken cancellationToken);

    Task<long> DeleteUnreferencedFilesAsync(
        IReadOnlySet<string> referencedSha256,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);

    Task<long> DeleteUnretainedArchivesAsync(
        IReadOnlySet<Guid> retainedRevisionIds,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);
}
