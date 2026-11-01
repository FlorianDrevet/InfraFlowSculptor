using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Applications;

public sealed class DeploymentStrategyRegistry
{
    private readonly Registry<DeploymentStrategyKind, IDeploymentStrategy> registry;

    public DeploymentStrategyRegistry(IEnumerable<IDeploymentStrategy> strategies) =>
        registry = new Registry<DeploymentStrategyKind, IDeploymentStrategy>(strategies);

    public IReadOnlyCollection<DeploymentStrategyKind> Keys => registry.Keys;

    public bool Supports(DeploymentStrategyKind key) => registry.Supports(key);

    public IDeploymentStrategy Get(DeploymentStrategyKind key) => registry.Get(key);
}
