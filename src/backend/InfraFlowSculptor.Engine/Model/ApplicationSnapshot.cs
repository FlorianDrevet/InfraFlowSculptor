using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Model;

public sealed record ApplicationSnapshot(
    Guid Id,
    Guid ComponentId,
    Guid ResourceId,
    string Name,
    string CodeSourcePath,
    string DockerfilePath,
    string BuildContextPath,
    string ImageRepository,
    string? HealthCheckPath,
    DeploymentStrategyKind DeploymentStrategy,
    Guid? BuildRegistryEnvironmentId,
    IReadOnlyList<ApplicationStepDefinition> Steps)
{
    public int Version { get; init; } = 1;
}
