namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetResourcePresenceRequest(Guid TargetId, bool IsPresent, int ExpectedVersion);
