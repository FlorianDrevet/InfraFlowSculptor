using System.Net;
using System.Net.Sockets;
using InfraFlowSculptor.Application.Naming.Availability;

namespace InfraFlowSculptor.Infrastructure.Dns;

public sealed class DnsNameAvailabilityProbe(
    Func<string, CancellationToken, Task<IPAddress[]>> resolveHost,
    TimeSpan timeout) : INameAvailabilityProbe
{
    public async Task<NameAvailabilityProbeResult> ProbeAsync(
        string availabilityDomain,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var addresses = await resolveHost(
                $"{name.Trim().ToLowerInvariant()}.{availabilityDomain.Trim().ToLowerInvariant()}",
                timeoutSource.Token);
            return new NameAvailabilityProbeResult(
                addresses.Length == 0 ? NameAvailabilityOutcome.NotResolved : NameAvailabilityOutcome.Resolved);
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.HostNotFound)
        {
            return new NameAvailabilityProbeResult(NameAvailabilityOutcome.NotResolved);
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TryAgain)
        {
            return new NameAvailabilityProbeResult(NameAvailabilityOutcome.Unknown, NameAvailabilityFailure.DnsServer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NameAvailabilityProbeResult(NameAvailabilityOutcome.Unknown, NameAvailabilityFailure.Timeout);
        }
        catch (SocketException)
        {
            return new NameAvailabilityProbeResult(NameAvailabilityOutcome.Unknown, NameAvailabilityFailure.DnsError);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new NameAvailabilityProbeResult(NameAvailabilityOutcome.Unknown, NameAvailabilityFailure.DnsError);
        }
    }
}
