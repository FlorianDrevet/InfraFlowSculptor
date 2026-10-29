using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Engine.Applications;

public interface IDeploymentStrategy : IKeyed<DeploymentStrategyKind>
{
    DeploymentStrategyPlan CreatePlan(ApplicationDefinition application);
}
