using System.Net;
using System.Text.Json.Nodes;

namespace InfraFlowSculptor.Api.Tests.OpenApi;

public sealed class OpenApiDocumentTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public OpenApiDocumentTests(ApiFactory factory)
    {
        factory.Environment = "Development";
        factory.UseTestSigningKey = false;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task BuildDocumentMatchesRuntimeOpenApiDocument()
    {
        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runtimeDocument = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var committedPath = FindRepositoryFile("src", "backend", "InfraFlowSculptor.Api", "openapi", "v1.json");
        var committedDocument = JsonNode.Parse(await File.ReadAllTextAsync(committedPath));

        Assert.True(JsonNode.DeepEquals(committedDocument, runtimeDocument), "Régénérez le document avec dotnet build.");
        Assert.Equal("GetVersion", runtimeDocument?["paths"]?["/v1/version"]?["get"]?["operationId"]?.GetValue<string>());
        Assert.Equal("GetMe", runtimeDocument?["paths"]?["/v1/me"]?["get"]?["operationId"]?.GetValue<string>());
    }

    private static string FindRepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. pathSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(pathSegments)} from the test output directory.");
    }
}
