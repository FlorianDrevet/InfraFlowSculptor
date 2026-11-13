using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class AzureDevOpsInstallKitPlatformEmitter : IInstallKitPlatformEmitter
{
    public CiPlatform Key => CiPlatform.AzureDevOps;

    public IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan, CatalogVersion catalog)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);

        var content = ReferenceInstallResources.Read("install.pipeline.yml", plan.Project.Code, plan.Project.Name);
        return [new GeneratedFile(".ifs/install/install.pipeline.yml", content)];
    }
}
