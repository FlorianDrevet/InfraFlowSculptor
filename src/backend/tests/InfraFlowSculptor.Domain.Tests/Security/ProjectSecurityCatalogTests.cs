using InfraFlowSculptor.Domain.Common.Security;
using Xunit;

namespace InfraFlowSculptor.Domain.Tests.Security;

public sealed class ProjectSecurityCatalogTests
{
    [Fact]
    public void PermissionCatalogExposesStableKeysForEveryPilotPermission()
    {
        var expected = new[]
        {
            "project.read",
            "audit.read",
            "model.edit",
            "applications.manage",
            "components.manage",
            "conventions.manage",
            "environments.manage",
            "environments.protected.manage",
            "publication.manage",
            "generate",
            "publish",
            "installation.manage",
            "proposals.apply",
            "members.manage",
            "project.administer"
        };

        Assert.Equal(expected, Permission.All);
    }
}
