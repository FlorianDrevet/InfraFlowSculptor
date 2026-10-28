namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed class NameAvailabilityService(
    INameAvailabilityStore store,
    TimeProvider clock)
{
    public async ValueTask<NameAvailabilityState?> GetStatusAsync(
        string availabilityDomain,
        string name,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var statuses = await GetStatusesAsync(availabilityDomain, [normalizedName], cancellationToken);
        return statuses.GetValueOrDefault(normalizedName);
    }

    public async Task<IReadOnlyDictionary<string, NameAvailabilityState?>> GetStatusesAsync(
        string availabilityDomain,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default)
    {
        var domain = availabilityDomain.Trim().ToLowerInvariant();
        var normalizedNames = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var statuses = new Dictionary<string, NameAvailabilityState?>(StringComparer.OrdinalIgnoreCase);
        if (normalizedNames.Length == 0)
        {
            return statuses;
        }

        var now = clock.GetUtcNow();
        var cached = await store.FindManyAsync(domain, normalizedNames, cancellationToken);
        var requeue = new List<string>();
        foreach (var name in normalizedNames)
        {
            if (!cached.TryGetValue(name, out var entry))
            {
                statuses[name] = null;
                requeue.Add(name);
                continue;
            }

            if (entry.State is NameAvailabilityState.Resolved or NameAvailabilityState.NotResolved
                && entry.ExpiresAt is { } expiresAt
                && expiresAt > now)
            {
                statuses[name] = entry.State;
                continue;
            }

            if (entry.State is NameAvailabilityState.Pending
                || entry.State is NameAvailabilityState.Unknown && entry.ExpiresAt is { } unknownExpiry && unknownExpiry > now)
            {
                statuses[name] = null;
                continue;
            }

            statuses[name] = null;
            requeue.Add(name);
        }

        if (requeue.Count > 0)
        {
            await store.EnqueueIfMissingOrExpiredAsync(domain, requeue, now, cancellationToken);
        }

        return statuses;
    }
}
