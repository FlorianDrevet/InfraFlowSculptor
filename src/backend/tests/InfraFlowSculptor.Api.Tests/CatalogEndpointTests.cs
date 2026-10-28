using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests;

public sealed class CatalogEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task MemberCanBrowseTheStaticCatalogWithoutOrganizationContext()
    {
        using var versionsResponse = await SendAsync(HttpMethod.Get, "/v1/catalog/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        using var versions = await JsonDocument.ParseAsync(await versionsResponse.Content.ReadAsStreamAsync());
        Assert.Contains(versions.RootElement.EnumerateArray(), version =>
            version.GetProperty("version").GetString() == "2026.10-pilot"
            && version.GetProperty("status").GetString() == "published");

        using var typesResponse = await SendAsync(HttpMethod.Get, "/v1/catalog/2026.10-pilot/types?category=security");
        Assert.Equal(HttpStatusCode.OK, typesResponse.StatusCode);
        using var types = await JsonDocument.ParseAsync(await typesResponse.Content.ReadAsStreamAsync());
        var securityTypes = types.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, securityTypes.Length);
        Assert.Contains(securityTypes, resourceType =>
            resourceType.GetProperty("type").GetString() == "KeyVault"
            && resourceType.GetProperty("labelFr").GetString() == "Coffre de clés"
            && resourceType.GetProperty("abbreviation").GetString() == "kv");
        Assert.Contains(securityTypes, resourceType =>
            resourceType.GetProperty("type").GetString() == "UserAssignedIdentity"
            && resourceType.GetProperty("labelFr").GetString() == "Identité managée affectée par l’utilisateur");

        var search = Uri.EscapeDataString("identité");
        using var searchResponse = await SendAsync(
            HttpMethod.Get,
            $"/v1/catalog/2026.10-pilot/types?search={search}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        using var searchResult = await JsonDocument.ParseAsync(await searchResponse.Content.ReadAsStreamAsync());
        Assert.Equal("UserAssignedIdentity", Assert.Single(searchResult.RootElement.EnumerateArray())
            .GetProperty("type").GetString());

        using var detailResponse = await SendAsync(HttpMethod.Get, "/v1/catalog/2026.10-pilot/types/KeyVault");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detail = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        Assert.Equal("KeyVault", detail.RootElement.GetProperty("type").GetString());
        Assert.True(detail.RootElement.TryGetProperty("generation", out _));
        Assert.True(detail.RootElement.TryGetProperty("pricing", out _));

        using var unknownVersion = await SendAsync(HttpMethod.Get, "/v1/catalog/2030.01-missing/types");
        using var unknownType = await SendAsync(HttpMethod.Get, "/v1/catalog/2026.10-pilot/types/Missing");
        Assert.Equal(HttpStatusCode.NotFound, unknownVersion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownType.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.CreateAccessToken(
                tenantId: "22222222-2222-2222-2222-222222222222",
                objectId: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
                displayName: "David Moreau",
                email: "david@fabrikam.example"));
        return await client.SendAsync(request);
    }
}
