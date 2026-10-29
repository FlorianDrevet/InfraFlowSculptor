using InfraFlowSculptor.Application.Naming.Availability;

namespace InfraFlowSculptor.Infrastructure.Persistence.Naming;

public sealed class NameAvailabilityEntity
{
    private NameAvailabilityEntity()
    {
    }

    public NameAvailabilityEntity(
        string availabilityDomain,
        string name,
        DateTimeOffset requestedAt)
    {
        AvailabilityDomain = availabilityDomain.Trim().ToLowerInvariant();
        Name = name.Trim().ToLowerInvariant();
        Status = NameAvailabilityState.Pending;
        RequestedAt = requestedAt;
        NextAttemptAt = requestedAt;
    }

    public string AvailabilityDomain { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public NameAvailabilityState Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? CheckedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public int Attempts { get; private set; }

    public NameAvailabilityFailure? Failure { get; private set; }

    public void Apply(NameAvailabilityUpdate update)
    {
        Status = update.State;
        NextAttemptAt = update.NextAttemptAt;
        CheckedAt = update.CheckedAt;
        ExpiresAt = update.ExpiresAt;
        Attempts = update.Attempts;
        Failure = update.Failure;
    }
}
