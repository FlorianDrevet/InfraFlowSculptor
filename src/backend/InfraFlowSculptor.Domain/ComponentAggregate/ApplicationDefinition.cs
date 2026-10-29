using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed record ApplicationDefinition(
    string Name,
    string CodeSourcePath,
    string DockerfilePath,
    string BuildContextPath,
    string ImageRepository,
    string? HealthCheckPath,
    DeploymentStrategyKind DeploymentStrategy,
    Guid? BuildRegistryEnvironmentId,
    IReadOnlyCollection<ApplicationStepDefinition> Steps);
