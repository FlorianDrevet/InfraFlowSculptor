namespace InfraFlowSculptor.Contracts.Components;

public sealed record SetComponentDefaultLogAnalyticsRequest(Guid? WorkspaceResourceId, int ExpectedVersion);
