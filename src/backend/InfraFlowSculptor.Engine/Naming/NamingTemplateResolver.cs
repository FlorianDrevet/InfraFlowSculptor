using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Naming;

internal static class NamingTemplateResolver
{
    public static ResolvedNamingTemplate Resolve(
        NamingConventionSnapshot project,
        ComponentNamingSnapshot? component,
        string resourceType,
        bool isResourceGroup)
    {
        if (component is not null
            && component.TypeTemplates.TryGetValue(resourceType, out var componentTypeTemplate))
        {
            return new ResolvedNamingTemplate("ComponentType", componentTypeTemplate);
        }

        if (!string.IsNullOrWhiteSpace(component?.DefaultTemplate))
        {
            return new ResolvedNamingTemplate("ComponentDefault", component.DefaultTemplate);
        }

        if (project.TypeTemplates.TryGetValue(resourceType, out var projectTypeTemplate))
        {
            return new ResolvedNamingTemplate("ProjectType", projectTypeTemplate);
        }

        return new ResolvedNamingTemplate(
            "ProjectDefault",
            isResourceGroup ? project.ResourceGroupTemplate : project.DefaultTemplate);
    }
}

internal sealed record ResolvedNamingTemplate(string Level, string Template);
