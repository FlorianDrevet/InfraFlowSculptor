using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Revisions;

public sealed class RevisionFilePurger(IIfsDbContext dbContext, IRevisionBlobStore blobStore)
{
    private const int MaximumBatchSize = 100;

    public async Task<int> PurgeAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var expiredRevisions = await dbContext.Query<RevisionAggregate>()
            .IgnoreQueryFilters()
            .Where(revision => revision.State == RevisionState.Generated
                && revision.PublishedAt == null
                && revision.CompletedAt < olderThan
                && revision.Files.Any())
            .OrderBy(revision => revision.CompletedAt)
            .ThenBy(revision => revision.Id)
            .Take(MaximumBatchSize)
            .ToArrayAsync(cancellationToken);

        foreach (var revision in expiredRevisions)
        {
            revision.PurgeFiles();
        }

        if (expiredRevisions.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var referencedSha256 = (await dbContext.Query<RevisionAggregate>()
                .IgnoreQueryFilters()
                .SelectMany(revision => revision.Files.Select(file => file.Sha256))
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var retainedRevisionIds = (await dbContext.Query<RevisionAggregate>()
                .IgnoreQueryFilters()
                .Where(revision => revision.State == RevisionState.Generated
                    && (revision.PublishedAt != null || revision.CompletedAt >= olderThan))
                .Select(revision => revision.Id)
                .ToArrayAsync(cancellationToken))
            .Select(id => id.Value)
            .ToHashSet();

        await blobStore.DeleteUnreferencedFilesAsync(referencedSha256, olderThan, cancellationToken);
        await blobStore.DeleteUnretainedArchivesAsync(retainedRevisionIds, olderThan, cancellationToken);
        return expiredRevisions.Length;
    }
}
