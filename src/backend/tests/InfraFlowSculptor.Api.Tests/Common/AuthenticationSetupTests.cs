using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests.Common;

public sealed class AuthenticationSetupTests
{
    [Fact]
    public void TestSigningKeyIsRejectedOutsideTesting()
    {
        using var factory = new ApiFactory { Environment = "Production" };

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
