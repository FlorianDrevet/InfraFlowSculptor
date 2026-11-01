using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class ReadmeEmitter
{
    public static GeneratedFile Emit(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var content = ReferenceInstallResources.Read("README.ifs.md");
        if (!string.Equals(plan.Project.Code, "shop", StringComparison.Ordinal))
        {
            content = content.Replace("shop", plan.Project.Code, StringComparison.Ordinal);
        }

        return new GeneratedFile("README.ifs.md", content);
    }
}
