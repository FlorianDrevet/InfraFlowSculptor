namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetExistingResourceIdRequest(Guid TargetId, string ResourceId, int ExpectedVersion);
