using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public interface IStepTranslator : IKeyed<CiPlatform>
{
    PipelineStepTranslation Translate(CatalogPipelineStepDescriptor descriptor, ApplicationStepPlan applicationStep);
}
