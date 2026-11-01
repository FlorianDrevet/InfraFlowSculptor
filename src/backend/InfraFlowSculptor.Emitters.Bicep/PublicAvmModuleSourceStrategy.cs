using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

public sealed class PublicAvmModuleSourceStrategy : IModuleSourceStrategy
{
    public ModuleSource Key => ModuleSource.PublicAvmRegistry;

    public string GetModuleReference(DeploymentPlan plan, CatalogResourceTypeDescriptor resourceType) =>
        resourceType.Definition
            .GetProperty("generation")
            .GetProperty("Bicep")
            .GetProperty("module")
            .GetString()!;
}
