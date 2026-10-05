using System.Net;
using System.Text.Json;

namespace InfraFlowSculptor.Api.Tests.System;

public sealed class VersionEndpointTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public VersionEndpointTests(ApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVersionReturnsSuccess()
    {
        using var response = await client.GetAsync("/v1/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRouteReturnsProblemDetailsWithTraceId()
    {
        using var response = await client.GetAsync("/v1/unknown-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var problem = await JsonDocument.ParseAsync(stream);

        Assert.True(problem.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }
}
