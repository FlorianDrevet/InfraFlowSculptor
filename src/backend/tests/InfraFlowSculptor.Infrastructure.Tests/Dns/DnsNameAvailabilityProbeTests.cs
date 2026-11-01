using System.Net;
using System.Net.Sockets;
using InfraFlowSculptor.Application.Naming.Availability;
using InfraFlowSculptor.Infrastructure.Dns;

namespace InfraFlowSculptor.Infrastructure.Tests.Dns;

public sealed class DnsNameAvailabilityProbeTests
{
    [Fact]
    public async Task ResolvesTheNormalizedNameAndDomain()
    {
        string? queriedHost = null;
        var probe = new DnsNameAvailabilityProbe(
            (host, _) =>
            {
                queriedHost = host;
                return Task.FromResult(new[] { IPAddress.Loopback });
            },
            TimeSpan.FromSeconds(1));

        var result = await probe.ProbeAsync("VAULT.AZURE.NET", "IFS-Shop-Dev");

        Assert.Equal("ifs-shop-dev.vault.azure.net", queriedHost);
        Assert.Equal(NameAvailabilityOutcome.Resolved, result.Outcome);
    }

    [Fact]
    public async Task MapsHostNotFoundToNotResolved()
    {
        var probe = new DnsNameAvailabilityProbe(
            (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)),
            TimeSpan.FromSeconds(1));

        var result = await probe.ProbeAsync("vault.azure.net", "ifs-shop-dev");

        Assert.Equal(NameAvailabilityOutcome.NotResolved, result.Outcome);
    }

    [Fact]
    public async Task MapsTimeoutToUnknownWithoutClaimingAvailability()
    {
        var probe = new DnsNameAvailabilityProbe(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return [];
            },
            TimeSpan.FromMilliseconds(25));

        var result = await probe.ProbeAsync("vault.azure.net", "ifs-shop-dev");

        Assert.Equal(NameAvailabilityOutcome.Unknown, result.Outcome);
        Assert.Equal(NameAvailabilityFailure.Timeout, result.Failure);
    }
}
