using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Isolation;

public sealed class EndpointIsolationTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory factory;
    private readonly HttpClient client;

    public EndpointIsolationTests(ApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    public Task InitializeAsync() => IsolationFixture.SeedAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EveryVersionedRouteHasAnIsolationScenario()
    {
        var dataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var routeEndpoints = dataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/v1", StringComparison.Ordinal) == true)
            .ToArray();
        var missingScenarios = routeEndpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(name => name is null || !IsolationScenarios.All.ContainsKey(name))
            .Select(name => name ?? "(unnamed)")
            .ToArray();

        Assert.Empty(missingScenarios);
        Assert.Equal(routeEndpoints.Length, IsolationScenarios.All.Count);

        foreach (var scenario in IsolationScenarios.All.Values)
        {
            using var request = scenario.CreateRequest("ignored");
            using var response = await client.SendAsync(request);
            Assert.Equal(scenario.ExpectedStatusCode, response.StatusCode);
        }
    }

    [Fact]
    public async Task DavidListsOnlyHisFabrikamOrganization()
    {
        var scenario = IsolationScenarios.All["ListMyOrganizations"];
        using var request = scenario.CreateRequest("ignored");
        using var response = await client.SendAsync(request);
        var organizations = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(global::System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(organizations);
        Assert.Single(organizations);
        Assert.Equal(IsolationFixture.FabrikamId, organizations[0].GetProperty("id").GetGuid());
    }
}
