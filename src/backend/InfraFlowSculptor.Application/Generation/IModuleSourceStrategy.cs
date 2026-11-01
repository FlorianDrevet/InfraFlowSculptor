using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public interface IModuleSourceStrategy : IKeyed<ModuleSource>
{
    string GetModuleReference(DeploymentPlan plan, CatalogResourceTypeDescriptor resourceType);
}
