using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public sealed class InstallKitEmitter
{
    public static IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var checklist = plan.InstallationChecklist ?? InstallationChecklistBuilder.Build(plan);
        return ReferenceInstallResources.ReadAll(plan.Project.Code, plan.Project.Name)
            .Select(file => file.RelativePath == ".ifs/install/SETUP.md"
                ? file with { Content = InstallationChecklistMarkdownRenderer.Append(file.Content, checklist) }
                : file)
            .ToArray();
    }
}
