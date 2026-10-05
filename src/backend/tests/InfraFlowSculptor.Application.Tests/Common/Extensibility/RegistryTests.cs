using InfraFlowSculptor.Application.Common.Extensibility;
using Xunit;

namespace InfraFlowSculptor.Application.Tests.Common.Extensibility;

public sealed class RegistryTests
{
    [Fact]
    public void GetThrowsAnErrorThatIncludesTheMissingKey()
    {
        var registry = new Registry<string, TestService>([new TestService("bicep")]);

        var exception = Assert.Throws<KeyNotFoundException>(() => registry.Get("terraform"));

        Assert.Contains("terraform", exception.Message, StringComparison.Ordinal);
    }

    private sealed record TestService(string Key) : IKeyed<string>;
}
