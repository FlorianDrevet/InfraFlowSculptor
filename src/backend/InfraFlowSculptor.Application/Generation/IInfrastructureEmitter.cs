using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public interface IInfrastructureEmitter : IKeyed<EmitterKey>
{
    IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan, CatalogVersion catalog);
}
