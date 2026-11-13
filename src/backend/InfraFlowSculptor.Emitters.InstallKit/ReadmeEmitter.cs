using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class ReadmeEmitter
{
    public static GeneratedFile Emit(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var content = ReferenceInstallResources.Read("README.ifs.md", plan.Project.Code, plan.Project.Name);

        return new GeneratedFile("README.ifs.md", content);
    }
}
