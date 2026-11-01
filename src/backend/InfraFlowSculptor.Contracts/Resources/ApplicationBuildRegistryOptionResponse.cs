namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationBuildRegistryOptionResponse(Guid EnvironmentId, string EnvironmentCode, string RegistryName);
