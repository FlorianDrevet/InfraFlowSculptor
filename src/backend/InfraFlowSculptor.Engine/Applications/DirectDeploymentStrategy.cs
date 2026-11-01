using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Applications;

public sealed class DirectDeploymentStrategy : IDeploymentStrategy
{
    public DeploymentStrategyKind Key => DeploymentStrategyKind.Direct;

    public DeploymentStrategyPlan CreatePlan(ApplicationDefinition application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var steps = new List<DeploymentStageStepKind> { DeploymentStageStepKind.DeployContainerImage };
        if (!string.IsNullOrWhiteSpace(application.HealthCheckPath))
        {
            steps.Add(DeploymentStageStepKind.CheckHealth);
        }

        return new DeploymentStrategyPlan([], steps);
    }
}
