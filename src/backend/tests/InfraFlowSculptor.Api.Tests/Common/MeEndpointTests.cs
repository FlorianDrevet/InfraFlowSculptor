using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests.Common;

public sealed class MeEndpointTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public MeEndpointTests(ApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMeWithoutTokenReturnsUnauthorizedProblemDetails()
    {
        using var response = await client.GetAsync("/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMeWithUnsupportedApiTokenReturnsUnauthorized()
    {
        using var request = CreateRequest("ifs_not-a-valid-token");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMeWithAliceTokenReturnsHerIdentityAndVerifiedEmail()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken());
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("11111111-1111-1111-1111-111111111111", profile.GetProperty("tenantId").GetString());
        Assert.Equal("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", profile.GetProperty("objectId").GetString());
        Assert.Equal("Alice Martin", profile.GetProperty("displayName").GetString());
        Assert.Equal("alice@contoso.example", profile.GetProperty("verifiedEmail").GetString());
    }

    [Fact]
    public async Task GetMeWithNinaTokenReturnsNoVerifiedEmail()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken(
            objectId: "ffffffff-ffff-4fff-8fff-ffffffffffff",
            displayName: "Nina Moreau",
            email: "nina@contoso.example",
            emailVerified: false));
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("verifiedEmail").ValueKind);
    }

    [Fact]
    public async Task GetMeWithVerifiedPrimaryEmailUsesThatAddress()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken(
            email: "unverified@contoso.example",
            emailVerified: false,
            verifiedPrimaryEmail: "primary@contoso.example"));
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("primary@contoso.example", profile.GetProperty("verifiedEmail").GetString());
    }

    [Fact]
    public async Task GetMeWithEntraVerifiedEmailUsesTheAddress()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken(
            email: "entra-verified@contoso.example",
            emailVerified: false,
            entraEmailVerified: true));
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("entra-verified@contoso.example", profile.GetProperty("verifiedEmail").GetString());
    }

    [Fact]
    public async Task GetMeWithPersonalAccountUsesItsEmail()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken(
            tenantId: "9188040d-6c67-4c5b-b112-36a304b66dad",
            email: "personal@outlook.example",
            emailVerified: false));
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("personal@outlook.example", profile.GetProperty("verifiedEmail").GetString());
    }

    [Fact]
    public async Task GetMePrefersVerifiedPrimaryEmailOverOtherVerifiedClaims()
    {
        using var request = CreateRequest(TestTokens.CreateAccessToken(
            email: "email@contoso.example",
            verifiedPrimaryEmail: "primary@contoso.example",
            entraEmailVerified: true));
        using var response = await client.SendAsync(request);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("primary@contoso.example", profile.GetProperty("verifiedEmail").GetString());
    }

    private static HttpRequestMessage CreateRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
