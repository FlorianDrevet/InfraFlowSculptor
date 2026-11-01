namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed record PendingNameAvailability(
    string AvailabilityDomain,
    string Name,
    int Attempts);
