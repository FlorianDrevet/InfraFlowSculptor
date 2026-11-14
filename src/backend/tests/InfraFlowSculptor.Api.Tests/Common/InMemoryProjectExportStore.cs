using System.Collections.Concurrent;
using System.Security.Cryptography;
using InfraFlowSculptor.Application.Generation;

namespace InfraFlowSculptor.Api.Tests.Common;

public sealed class InMemoryProjectExportStore(TimeProvider clock) : IProjectExportStore
{
    private readonly ConcurrentDictionary<string, ExportEntry> exports = new(StringComparer.Ordinal);

    public Task<string> SaveAsync(
        ReadOnlyMemory<byte> content,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        exports[token] = new ExportEntry(content.ToArray(), expiresAt);
        return Task.FromResult(token);
    }

    public Task<Stream?> OpenAsync(string token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!exports.TryGetValue(token, out var entry))
        {
            return Task.FromResult<Stream?>(null);
        }

        if (entry.ExpiresAt <= now || entry.ExpiresAt <= clock.GetUtcNow())
        {
            exports.TryRemove(token, out _);
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new MemoryStream(entry.Content, writable: false));
    }

    public Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var export in exports.Where(export => export.Value.ExpiresAt <= now).ToArray())
        {
            exports.TryRemove(export.Key, out _);
        }

        return Task.CompletedTask;
    }

    private sealed record ExportEntry(byte[] Content, DateTimeOffset ExpiresAt);
}
