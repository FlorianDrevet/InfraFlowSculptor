namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed record NameAvailabilityUpdate(
    NameAvailabilityState State,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? CheckedAt,
    DateTimeOffset? ExpiresAt,
    int Attempts,
    NameAvailabilityFailure? Failure);
