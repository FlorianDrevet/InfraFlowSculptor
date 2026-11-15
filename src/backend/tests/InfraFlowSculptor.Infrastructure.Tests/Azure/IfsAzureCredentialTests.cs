using InfraFlowSculptor.Infrastructure.Azure;

namespace InfraFlowSculptor.Infrastructure.Tests.Azure;

public sealed class IfsAzureCredentialTests
{
    [Fact]
    public void DefaultCredentialDisablesInteractiveBrowserWithoutDisablingInstanceDiscovery()
    {
        var options = IfsAzureCredential.CreateDefaultCredentialOptions();

        Assert.True(options.ExcludeInteractiveBrowserCredential);
        Assert.False(options.DisableInstanceDiscovery);
    }
}
