using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Architecture.Tests;

internal static class ExtensionKeys
{
    // Add each extension-key enum here in the step that introduces it.
    internal static IReadOnlyList<Type> Types { get; } = [typeof(LinkKind), typeof(DataEngine), typeof(DeploymentStrategyKind)];
}
