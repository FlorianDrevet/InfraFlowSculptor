using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Infrastructure.Azure;

public sealed partial class AzureRevisionBlobStore(BlobServiceClient serviceClient, TimeProvider clock) : IRevisionBlobStore
{
    private const string ContainerName = "revisions";
    private const string LastUsedMetadataName = "ifs_last_used_utc";
    private static readonly DateTimeOffset FixedArchiveTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly object containerLock = new();
    private Task? containerInitialization;

    public async Task EnsureFileAsync(
        string sha256,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        ValidateSha256(sha256);
        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(GetFilePath(sha256));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await blob.UploadAsync(BinaryData.FromBytes(content), overwrite: false, cancellationToken);
                return;
            }
            catch (RequestFailedException exception) when (exception.Status is 409 or 412)
            {
                if (await TouchExistingBlobAsync(blob, cancellationToken))
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException("The content-addressed revision file could not be retained.");
    }

    public async Task<Stream> OpenFileAsync(string sha256, CancellationToken cancellationToken)
    {
        ValidateSha256(sha256);
        var container = await GetContainerAsync(cancellationToken);
        var result = await container.GetBlobClient(GetFilePath(sha256))
            .DownloadStreamingAsync(cancellationToken: cancellationToken);
        return result.Value.Content;
    }

    public async Task<Stream> GetOrCreateArchiveAsync(
        Guid revisionId,
        IReadOnlyCollection<RevisionFile> files,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("A revision identifier is required.", nameof(revisionId));
        }

        var container = await GetContainerAsync(cancellationToken);
        var archiveBlob = container.GetBlobClient($"archives/{revisionId:N}.zip");
        if (await archiveBlob.ExistsAsync(cancellationToken)
            && await TouchExistingBlobAsync(archiveBlob, cancellationToken))
        {
            return await DownloadArchiveAsync(archiveBlob, cancellationToken);
        }

        await using var archiveContent = new MemoryStream();
        using (var archive = new ZipArchive(archiveContent, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(file.RelativePath, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedArchiveTimestamp;
                await using var source = await OpenFileAsync(file.Sha256, cancellationToken);
                await using var destination = entry.Open();
                await source.CopyToAsync(destination, cancellationToken);
            }
        }

        archiveContent.Position = 0;
        try
        {
            await archiveBlob.UploadAsync(BinaryData.FromStream(archiveContent), overwrite: false, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            // Another request created the same immutable revision archive.
        }

        _ = await TouchExistingBlobAsync(archiveBlob, cancellationToken);
        return await DownloadArchiveAsync(archiveBlob, cancellationToken);
    }

    public async Task<long> DeleteUnreferencedFilesAsync(
        IReadOnlySet<string> referencedSha256,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(referencedSha256);
        var container = await GetContainerAsync(cancellationToken);
        long deleted = 0;
        await foreach (var item in container.GetBlobsAsync(
                           BlobTraits.None,
                           BlobStates.None,
                           "sha256/",
                           cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lastSeparator = item.Name.LastIndexOf('/');
            var sha256 = lastSeparator < 0 ? item.Name : item.Name[(lastSeparator + 1)..];
            if (!Sha256Pattern().IsMatch(sha256)
                || referencedSha256.Contains(sha256)
                || item.Properties.LastModified is not { } lastModified
                || lastModified >= olderThan
                || item.Properties.ETag is not { } etag)
            {
                continue;
            }

            try
            {
                var result = await container.GetBlobClient(item.Name).DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    new BlobRequestConditions { IfMatch = etag },
                    cancellationToken);
                if (result.Value)
                {
                    deleted++;
                }
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 412)
            {
                // A missing or concurrently touched blob does not need another purge attempt.
            }
        }

        return deleted;
    }

    public async Task<long> DeleteUnretainedArchivesAsync(
        IReadOnlySet<Guid> retainedRevisionIds,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(retainedRevisionIds);
        var container = await GetContainerAsync(cancellationToken);
        long deleted = 0;
        await foreach (var item in container.GetBlobsAsync(
                           BlobTraits.None,
                           BlobStates.None,
                           "archives/",
                           cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = item.Name[(item.Name.LastIndexOf('/') + 1)..];
            var archiveName = Path.GetFileNameWithoutExtension(fileName);
            if (!Guid.TryParseExact(archiveName, "N", out var revisionId)
                || retainedRevisionIds.Contains(revisionId)
                || item.Properties.LastModified is not { } lastModified
                || lastModified >= olderThan
                || item.Properties.ETag is not { } etag)
            {
                continue;
            }

            try
            {
                var result = await container.GetBlobClient(item.Name).DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    new BlobRequestConditions { IfMatch = etag },
                    cancellationToken);
                if (result.Value)
                {
                    deleted++;
                }
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 412)
            {
                // A missing or concurrently touched archive does not need another purge attempt.
            }
        }

        return deleted;
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        await GetContainerInitializationTask().WaitAsync(cancellationToken);
        return serviceClient.GetBlobContainerClient(ContainerName);
    }

    private Task GetContainerInitializationTask()
    {
        lock (containerLock)
        {
            if (containerInitialization is null || containerInitialization.IsFaulted || containerInitialization.IsCanceled)
            {
                containerInitialization = CreateContainerAsync();
            }

            return containerInitialization;
        }
    }

    private async Task CreateContainerAsync()
    {
        await serviceClient.GetBlobContainerClient(ContainerName).CreateIfNotExistsAsync(PublicAccessType.None);
    }

    private async Task<bool> TouchExistingBlobAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
                var metadata = new Dictionary<string, string>(properties.Value.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    [LastUsedMetadataName] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)
                };
                await blob.SetMetadataAsync(
                    metadata,
                    new BlobRequestConditions { IfMatch = properties.Value.ETag },
                    cancellationToken);
                return true;
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 412)
            {
                if (exception.Status == 404)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static async Task<Stream> DownloadArchiveAsync(
        BlobClient blob,
        CancellationToken cancellationToken)
    {
        var result = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return result.Value.Content;
    }

    private static string GetFilePath(string sha256) => $"sha256/{sha256[..2]}/{sha256}";

    private static void ValidateSha256(string sha256)
    {
        if (!Sha256Pattern().IsMatch(sha256))
        {
            throw new ArgumentException("The file hash must be a SHA-256 value.", nameof(sha256));
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
