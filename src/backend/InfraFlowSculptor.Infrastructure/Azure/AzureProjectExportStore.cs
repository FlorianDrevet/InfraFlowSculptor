using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InfraFlowSculptor.Application.Generation;

namespace InfraFlowSculptor.Infrastructure.Azure;

public sealed partial class AzureProjectExportStore(BlobServiceClient serviceClient, TimeProvider clock) : IProjectExportStore
{
    private const string ContainerName = "revisions";
    private const string Prefix = "exports/";
    private const string ExpiresAtMetadataName = "ifs_expires_at_utc";
    private readonly object containerLock = new();
    private Task? containerInitialization;

    public async Task<string> SaveAsync(
        ReadOnlyMemory<byte> content,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        if (expiresAt <= clock.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "The export link expiration must be in the future.");
        }

        var container = await GetContainerAsync(cancellationToken);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json; charset=utf-8",
                    CacheControl = "no-store"
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ExpiresAtMetadataName] = expiresAt.ToString("O", CultureInfo.InvariantCulture)
                }
            };

            try
            {
                await container.GetBlobClient(GetBlobNameForToken(token))
                    .UploadAsync(BinaryData.FromBytes(content), options, cancellationToken);
                return token;
            }
            catch (RequestFailedException exception) when (exception.Status is 409 or 412)
            {
                // A token collision is improbable, but retry without replacing an existing export.
            }
        }

        throw new InvalidOperationException("A unique project export token could not be allocated.");
    }

    public async Task<Stream?> OpenAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TokenPattern().IsMatch(token))
        {
            return null;
        }

        var blob = (await GetContainerAsync(cancellationToken)).GetBlobClient(GetBlobNameForToken(token));
        BlobProperties properties;
        try
        {
            properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }

        if (!properties.Metadata.TryGetValue(ExpiresAtMetadataName, out var expiresAtValue)
            || !DateTimeOffset.TryParse(
                expiresAtValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt)
            || expiresAt <= now)
        {
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
            return null;
        }

        try
        {
            var result = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return result.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        await foreach (var item in container.GetBlobsAsync(
                           BlobTraits.Metadata,
                           BlobStates.None,
                           Prefix,
                           cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Properties.ETag is not { } etag
                || item.Metadata is null
                || !item.Metadata.TryGetValue(ExpiresAtMetadataName, out var expiresAtValue)
                || !DateTimeOffset.TryParse(
                    expiresAtValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var expiresAt)
                || expiresAt > now)
            {
                continue;
            }

            try
            {
                await container.GetBlobClient(item.Name).DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    new BlobRequestConditions { IfMatch = etag },
                    cancellationToken);
            }
            catch (RequestFailedException exception) when (exception.Status is 404 or 412)
            {
                // The export disappeared or was changed after the listing.
            }
        }
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

    private async Task CreateContainerAsync() =>
        await serviceClient.GetBlobContainerClient(ContainerName).CreateIfNotExistsAsync(PublicAccessType.None);

    internal static string GetBlobNameForToken(string token)
    {
        var tokenBytes = Convert.FromHexString(token);
        var tokenDigest = SHA256.HashData(tokenBytes);
        return $"{Prefix}{Convert.ToHexString(tokenDigest).ToLowerInvariant()}.json";
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
