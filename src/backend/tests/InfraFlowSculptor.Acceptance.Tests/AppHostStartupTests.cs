using System.Net;

namespace InfraFlowSculptor.Acceptance.Tests;

[Collection(JobsTestCollection.Name)]
public sealed class AppHostStartupTests(JobsTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task ApiIsHealthyWhenAllEmulatorsRun()
    {
        using var response = await fixture.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
