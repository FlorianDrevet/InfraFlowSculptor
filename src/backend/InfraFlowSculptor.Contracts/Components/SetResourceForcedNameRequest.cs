namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetResourceForcedNameRequest(Guid TargetId, string Name, int ExpectedVersion);
