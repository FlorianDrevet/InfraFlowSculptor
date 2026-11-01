namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ApplicationConfigurationResponse(
    int Version,
    string Name,
    string CodeSourcePath,
    string DockerfilePath,
    string BuildContextPath,
    string ImageRepository,
    string? HealthCheckPath,
    string DeploymentStrategy,
    ApplicationBuildRegistryResponse BuildRegistry,
    IReadOnlyList<ApplicationStepResponse> Steps,
    IReadOnlyList<ApplicationFindingResponse> Findings);
