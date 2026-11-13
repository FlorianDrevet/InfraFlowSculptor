using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class InstallKitEmitter
{
    public static IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ReferenceInstallResources.ReadAll(plan.Project.Code, plan.Project.Name);
    }
}
