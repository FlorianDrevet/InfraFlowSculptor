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

        var content = ReferenceInstallResources.Read("install.pipeline.yml");
        if (!string.Equals(plan.Project.Code, "shop", StringComparison.Ordinal))
        {
            content = content.Replace("shop", plan.Project.Code, StringComparison.Ordinal);
        }

        return [new GeneratedFile(".ifs/install/install.pipeline.yml", content)];
    }
}
