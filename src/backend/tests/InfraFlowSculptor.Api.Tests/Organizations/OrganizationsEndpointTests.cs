using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Organizations;

public sealed class OrganizationsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task AuthenticatedUserCanCreateOrganization()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name = "Contoso Labs" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateAccessToken());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var organization = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Contoso Labs", organization.GetProperty("name").GetString());
        Assert.Contains(
            "Administrator",
            organization.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("This organization name is longer than eighty characters and must be rejected by validation")]
    public async Task CreateOrganizationRejectsNamesOutsideTheAllowedLength(string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateAccessToken());

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCanListAndRenameAnOrganizationTheyBelongTo()
    {
        var token = TestTokens.CreateAccessToken(objectId: "99999999-1111-4111-8111-111111111111");
        var organizationId = await CreateOrganizationAsync(token, "Contoso Labs");

        using var listRequest = CreateRequest(HttpMethod.Get, "/v1/organizations", token);
        using var listResponse = await client.SendAsync(listRequest);
        var organizations = await listResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(organizations);
        Assert.Contains(organizations, organization => organization.GetProperty("id").GetGuid() == organizationId);

        using var missingHeaderRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}",
            token);
        using var missingHeaderResponse = await client.SendAsync(missingHeaderRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingHeaderResponse.StatusCode);

        using var renameRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}",
            token,
            new { name = "Contoso Labs Europe" });
        renameRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var renameResponse = await client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var renamed = await renameResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Contoso Labs Europe", renamed.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UserOutsideAnOrganizationGetsNotFoundForItsDetails()
    {
        var ownerToken = TestTokens.CreateAccessToken(objectId: "99999999-2222-4222-8222-222222222222");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Private");
        var otherUserToken = TestTokens.CreateAccessToken(
            tenantId: "22222222-2222-2222-2222-222222222222",
            objectId: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            displayName: "David Moreau",
            email: "david@fabrikam.example");

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}",
            otherUserToken);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationMemberWithoutAdministratorRoleCannotRename()
    {
        const string ownerObjectId = "99999999-3333-4333-8333-333333333333";
        const string memberObjectId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId);
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Members");

        using var meRequest = CreateRequest(HttpMethod.Get, "/v1/me", memberToken);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var owner = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(ownerObjectId));
            var member = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(memberObjectId));
            var organization = await dbContext.Organizations
                .Include(candidate => candidate.Members)
                .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));
            Assert.False(organization.AddMember(member.Id, [OrganizationRole.Member]).IsError);
            await dbContext.SaveChangesAsync();
            Assert.NotEqual(owner.Id, member.Id);
        }

        using var renameRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}",
            memberToken,
            new { name = "Member Rename" });
        renameRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var response = await client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> CreateOrganizationAsync(string token, string name)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v1/organizations", token, new { name });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var organization = await response.Content.ReadFromJsonAsync<JsonElement>();
        return organization.GetProperty("id").GetGuid();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
