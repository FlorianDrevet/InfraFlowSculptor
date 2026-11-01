using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Application.Generation;

namespace InfraFlowSculptor.Architecture.Tests;

internal static class ExtensionKeys
{
    // Add each extension-key enum here in the step that introduces it.
    internal static IReadOnlyList<Type> Types { get; } =
    [
        typeof(LinkKind),
        typeof(CiPlatform),
        typeof(DataEngine),
        typeof(DeploymentStrategyKind),
        typeof(IacLanguage),
        typeof(PulumiLanguage),
        typeof(ModuleSource)
    ];
}
