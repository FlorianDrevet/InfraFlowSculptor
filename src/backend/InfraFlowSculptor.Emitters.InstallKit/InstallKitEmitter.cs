using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class InstallKitEmitter
{
    public static IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return
        [
            new GeneratedFile(".ifs/install/azure-setup.ps1", ReferenceInstallResources.Read("azure-setup.ps1")),
            new GeneratedFile(".ifs/install/SETUP.md", RenderProjectCode(ReferenceInstallResources.Read("SETUP.md"), plan.Project.Code))
        ];
    }

    private static string RenderProjectCode(string content, string projectCode) =>
        string.Equals(projectCode, "shop", StringComparison.Ordinal)
            ? content
            : content.Replace("shop", projectCode, StringComparison.Ordinal);
}
