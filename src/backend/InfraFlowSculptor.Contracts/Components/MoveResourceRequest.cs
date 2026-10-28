namespace InfraFlowSculptor.Contracts.Components;

public sealed record MoveResourceRequest(Guid ResourceGroupId, int ExpectedVersion);
