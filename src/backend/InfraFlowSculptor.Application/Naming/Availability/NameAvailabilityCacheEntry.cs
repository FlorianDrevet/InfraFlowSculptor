namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed record NameAvailabilityCacheEntry(
    NameAvailabilityState State,
    DateTimeOffset? ExpiresAt);
