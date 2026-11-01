using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public interface IPipelineEmitter : IKeyed<CiPlatform>
{
    IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan, CatalogVersion catalog);
}
