namespace InfraFlowSculptor.Application.Naming.Availability;

public interface INameAvailabilityProbe
{
    Task<NameAvailabilityProbeResult> ProbeAsync(
        string availabilityDomain,
        string name,
        CancellationToken cancellationToken = default);
}
