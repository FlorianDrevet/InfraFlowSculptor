using InfraFlowSculptor.Application.Naming.Availability;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Naming;

public sealed class EfNameAvailabilityStore(IfsDbContext dbContext) : INameAvailabilityStore
{
    public async Task<IReadOnlyDictionary<string, NameAvailabilityCacheEntry>> FindManyAsync(
        string availabilityDomain,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default)
    {
        var domain = Normalize(availabilityDomain);
        var normalizedNames = names.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalizedNames.Length == 0)
        {
            return new Dictionary<string, NameAvailabilityCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        var entries = await dbContext.Set<NameAvailabilityEntity>()
            .Where(candidate => candidate.AvailabilityDomain == domain && normalizedNames.Contains(candidate.Name))
            .Select(candidate => new { candidate.Name, candidate.Status, candidate.ExpiresAt })
            .ToArrayAsync(cancellationToken);
        return entries.ToDictionary(
            entry => entry.Name,
            entry => new NameAvailabilityCacheEntry(entry.Status, entry.ExpiresAt),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task EnqueueIfMissingOrExpiredAsync(
        string availabilityDomain,
        IReadOnlyList<string> names,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var domain = Normalize(availabilityDomain);
        var normalizedNames = names.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalizedNames.Length == 0)
        {
            return;
        }

        var pending = NameAvailabilityState.Pending.ToString();
        var resolved = NameAvailabilityState.Resolved.ToString();
        var notResolved = NameAvailabilityState.NotResolved.ToString();
        var unknown = NameAvailabilityState.Unknown.ToString();

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO name_availability (
                availability_domain, name, status, requested_at, next_attempt_at, attempts)
            SELECT {domain}, requested.name, {pending}, {requestedAt}, {requestedAt}, 0
            FROM unnest({normalizedNames}) AS requested(name)
            ON CONFLICT (availability_domain, name) DO UPDATE
            SET status = {pending},
                requested_at = {requestedAt},
                next_attempt_at = {requestedAt},
                checked_at = NULL,
                expires_at = NULL,
                attempts = 0,
                failure_code = NULL
            WHERE name_availability.status IN ({resolved}, {notResolved}, {unknown})
                AND name_availability.expires_at <= {requestedAt}
            """, cancellationToken);
    }

    public async Task DeleteExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Set<NameAvailabilityEntity>()
            .Where(entry => entry.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingNameAvailability>> FindPendingAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<NameAvailabilityEntity>()
            .Where(entry => entry.Status == NameAvailabilityState.Pending && entry.NextAttemptAt <= now)
            .OrderBy(entry => entry.NextAttemptAt)
            .Take(limit)
            .Select(entry => new PendingNameAvailability(
                entry.AvailabilityDomain,
                entry.Name,
                entry.Attempts))
            .ToArrayAsync(cancellationToken);

    public async Task RecordAttemptAsync(
        string availabilityDomain,
        string name,
        NameAvailabilityUpdate update,
        CancellationToken cancellationToken = default)
    {
        var domain = Normalize(availabilityDomain);
        var normalizedName = Normalize(name);
        var entry = await dbContext.Set<NameAvailabilityEntity>()
            .SingleAsync(
                candidate => candidate.AvailabilityDomain == domain && candidate.Name == normalizedName,
                cancellationToken);
        entry.Apply(update);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
