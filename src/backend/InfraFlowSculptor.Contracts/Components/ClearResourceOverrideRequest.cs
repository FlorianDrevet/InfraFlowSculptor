namespace InfraFlowSculptor.Contracts.Components;

public sealed record ClearResourceOverrideRequest(Guid TargetId, string Property, int ExpectedVersion);
