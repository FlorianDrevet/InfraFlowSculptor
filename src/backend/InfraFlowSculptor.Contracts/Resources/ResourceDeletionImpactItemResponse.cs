namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceDeletionImpactItemResponse(ResourceDeletionImpactKind Kind, string Type, string Name);
