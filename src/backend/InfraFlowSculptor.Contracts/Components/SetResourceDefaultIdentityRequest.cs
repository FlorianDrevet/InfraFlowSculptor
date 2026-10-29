namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetResourceDefaultIdentityRequest(Guid? IdentityResourceId, int ExpectedVersion);
