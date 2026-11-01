using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ApplicationPlan(
    string Name,
    string Resource,
    string CodeSourcePath,
    string DockerfilePath,
    string BuildContextPath,
    string ImageRepository,
    string? HealthCheckPath,
    DeploymentStrategyKind Strategy,
    IReadOnlyList<ApplicationTargetPlan> Targets);
