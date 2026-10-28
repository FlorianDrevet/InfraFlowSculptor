namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed record NameAvailabilityProbeResult(
    NameAvailabilityOutcome Outcome,
    NameAvailabilityFailure? Failure = null);
