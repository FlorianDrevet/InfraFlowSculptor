using InfraFlowSculptor.Application.Naming.Availability;

namespace InfraFlowSculptor.Application.Tests.Naming;

public sealed class NameAvailabilityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReturnsFreshResolvedCacheWithoutSchedulingAnotherProbe()
    {
        var store = new FakeNameAvailabilityStore
        {
            Entries = new Dictionary<string, NameAvailabilityCacheEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["ifs-shop-main-dev"] = new(NameAvailabilityState.Resolved, Now.AddHours(12))
            }
        };
        var service = new NameAvailabilityService(store, new FrozenTimeProvider(Now));

        var state = await service.GetStatusAsync("vault.azure.net", "ifs-shop-main-dev");

        Assert.Equal(NameAvailabilityState.Resolved, state);
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task ReadsAndQueuesAnAvailabilityBatchOncePerDomain()
    {
        var store = new FakeNameAvailabilityStore
        {
            Entries = new Dictionary<string, NameAvailabilityCacheEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["ifs-shop-known"] = new(NameAvailabilityState.Resolved, Now.AddHours(12))
            }
        };
        var service = new NameAvailabilityService(store, new FrozenTimeProvider(Now));

        var statuses = await service.GetStatusesAsync("VAULT.AZURE.NET", ["IFS-SHOP-KNOWN", "IFS-SHOP-NEW"]);

        Assert.Equal(NameAvailabilityState.Resolved, statuses["ifs-shop-known"]);
        Assert.Null(statuses["ifs-shop-new"]);
        Assert.Single(store.BatchLookups);
        Assert.Equal(["ifs-shop-new"], Assert.Single(store.RequestBatches).Names);
    }

    [Fact]
    public async Task SchedulesMissingAndExpiredNamesButDoesNotDuplicatePendingRequests()
    {
        var store = new FakeNameAvailabilityStore();
        var service = new NameAvailabilityService(store, new FrozenTimeProvider(Now));

        var missing = await service.GetStatusAsync("VAULT.AZURE.NET", "IFS-SHOP-DEV");
        var pending = await service.GetStatusAsync("vault.azure.net", "ifs-shop-dev");

        Assert.Null(missing);
        Assert.Null(pending);
        var request = Assert.Single(store.Requests);
        Assert.Equal("vault.azure.net", request.Domain);
        Assert.Equal("ifs-shop-dev", request.Name);

        store.Entries = new Dictionary<string, NameAvailabilityCacheEntry>(store.Entries, StringComparer.OrdinalIgnoreCase)
        {
            ["ifs-shop-dev"] = new(NameAvailabilityState.Resolved, Now.AddTicks(-1))
        };
        await service.GetStatusAsync("vault.azure.net", "ifs-shop-dev");

        Assert.Equal(2, store.Requests.Count);
    }

    [Fact]
    public async Task UnknownResultRemainsNeutralUntilItsRetryWindowExpires()
    {
        var store = new FakeNameAvailabilityStore
        {
            Entries = new Dictionary<string, NameAvailabilityCacheEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["ifs-shop-dev"] = new(NameAvailabilityState.Unknown, Now.AddMinutes(30))
            }
        };
        var service = new NameAvailabilityService(store, new FrozenTimeProvider(Now));

        var state = await service.GetStatusAsync("vault.azure.net", "ifs-shop-dev");

        Assert.Null(state);
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task WorkerCachesDefinitiveResultForTwentyFourHours()
    {
        var store = new FakeNameAvailabilityStore
        {
            Pending = [new PendingNameAvailability("vault.azure.net", "ifs-shop-dev", 0)]
        };
        var probe = new FakeNameAvailabilityProbe(NameAvailabilityOutcome.Resolved);
        var worker = new NameAvailabilityProcessor(store, probe, new FrozenTimeProvider(Now));

        await worker.ProcessPendingAsync();

        var update = Assert.Single(store.Updates);
        Assert.Equal(1, store.DeleteExpiredCalls);
        Assert.Equal(NameAvailabilityState.Resolved, update.State);
        Assert.Equal(Now, update.CheckedAt);
        Assert.Equal(Now.AddHours(24), update.ExpiresAt);
        Assert.Equal(0, update.Attempts);
    }

    [Theory]
    [InlineData(0, NameAvailabilityState.Pending, 1, 1)]
    [InlineData(1, NameAvailabilityState.Pending, 2, 5)]
    [InlineData(2, NameAvailabilityState.Unknown, 3, 60)]
    public async Task UnknownProbeUsesBoundedBackoff(
        int attempts,
        NameAvailabilityState expectedState,
        int expectedAttempts,
        int expectedDelayMinutes)
    {
        var store = new FakeNameAvailabilityStore
        {
            Pending = [new PendingNameAvailability("vault.azure.net", "ifs-shop-dev", attempts)]
        };
        var probe = new FakeNameAvailabilityProbe(NameAvailabilityOutcome.Unknown);
        var worker = new NameAvailabilityProcessor(store, probe, new FrozenTimeProvider(Now));

        await worker.ProcessPendingAsync();

        var update = Assert.Single(store.Updates);
        Assert.Equal(expectedState, update.State);
        Assert.Equal(expectedAttempts, update.Attempts);
        Assert.Equal(Now.AddMinutes(expectedDelayMinutes), update.NextAttemptAt);
        Assert.Null(update.CheckedAt);
    }

    private sealed class FakeNameAvailabilityStore : INameAvailabilityStore
    {
        public Dictionary<string, NameAvailabilityCacheEntry> Entries { get; set; } =
            new Dictionary<string, NameAvailabilityCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<PendingNameAvailability> Pending { get; set; } = [];

        public List<(string Domain, string Name)> Requests { get; } = [];

        public List<(string Domain, IReadOnlyList<string> Names)> RequestBatches { get; } = [];

        public List<(string Domain, IReadOnlyList<string> Names)> BatchLookups { get; } = [];

        public List<NameAvailabilityUpdate> Updates { get; } = [];

        public int DeleteExpiredCalls { get; private set; }

        public Task<IReadOnlyDictionary<string, NameAvailabilityCacheEntry>> FindManyAsync(
            string availabilityDomain,
            IReadOnlyList<string> names,
            CancellationToken cancellationToken = default)
        {
            BatchLookups.Add((availabilityDomain, names));
            var entries = Entries.Where(entry => names.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, NameAvailabilityCacheEntry>>(entries);
        }

        public Task EnqueueIfMissingOrExpiredAsync(
            string availabilityDomain,
            IReadOnlyList<string> names,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken = default)
        {
            RequestBatches.Add((availabilityDomain, names));
            var updatedEntries = new Dictionary<string, NameAvailabilityCacheEntry>(Entries, StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                Requests.Add((availabilityDomain, name));
                updatedEntries[name] = new NameAvailabilityCacheEntry(NameAvailabilityState.Pending, null);
            }

            Entries = updatedEntries;

            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            DeleteExpiredCalls++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PendingNameAvailability>> FindPendingAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) => Task.FromResult(Pending);

        public Task RecordAttemptAsync(
            string availabilityDomain,
            string name,
            NameAvailabilityUpdate update,
            CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNameAvailabilityProbe(NameAvailabilityOutcome outcome) : INameAvailabilityProbe
    {
        public Task<NameAvailabilityProbeResult> ProbeAsync(
            string availabilityDomain,
            string name,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new NameAvailabilityProbeResult(outcome));
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
