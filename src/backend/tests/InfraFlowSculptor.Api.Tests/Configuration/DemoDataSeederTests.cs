using InfraFlowSculptor.Api.Configuration;

namespace InfraFlowSculptor.Api.Tests.Configuration;

public sealed class DemoDataSeederTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void DemoSeedingIsLimitedToNonProductionEnvironments(string environmentName, bool expected)
    {
        Assert.Equal(expected, DemoDataSeeder.IsAllowedEnvironment(environmentName));
    }
}
