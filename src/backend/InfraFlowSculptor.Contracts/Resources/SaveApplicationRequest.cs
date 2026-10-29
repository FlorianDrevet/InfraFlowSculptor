namespace InfraFlowSculptor.Contracts.Resources;

public sealed record SaveApplicationRequest(
    int ExpectedVersion,
    string Name,
    string CodeSourcePath,
    string DockerfilePath,
    string BuildContextPath,
    string ImageRepository,
    string? HealthCheckPath,
    string DeploymentStrategy,
    Guid? BuildRegistryEnvironmentId,
    IReadOnlyList<ApplicationStepRequest> Steps);
