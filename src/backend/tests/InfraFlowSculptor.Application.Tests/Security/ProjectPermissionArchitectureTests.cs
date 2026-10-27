using System.Reflection;
using InfraFlowSculptor.Application.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Tests.Security;

public sealed class ProjectPermissionArchitectureTests
{
    [Fact]
    public void EveryProjectScopedCommandAndQueryDeclaresARequiredPermission()
    {
        var missingRequirements = typeof(IProjectScopedRequest).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IMessage).IsAssignableFrom(type))
            .Where(type => type.GetProperty(nameof(IProjectScopedRequest.ProjectId))?.PropertyType == typeof(Guid))
            .Where(type => type.GetCustomAttribute<RequiresPermissionAttribute>() is null)
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(missingRequirements);
    }
}
