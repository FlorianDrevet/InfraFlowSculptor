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
    private static readonly string[] ConnectionManagerMemberRoles = ["ConnectionManager", "Member"];
    private static readonly string[] ContosoEmailDomains = ["contoso.example"];
    private static readonly string[] ContosoTenantIds = ["11111111-1111-4111-8111-111111111111"];
    private static readonly string[] ConnectionManagerOnly = ["ConnectionManager"];

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task AuthenticatedUserCanCreateOrganization()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name = "Contoso Labs" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateAccessToken());
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var organization = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Contoso Labs", organization.GetProperty("name").GetString());
        Assert.Contains(
            "Administrator",
            organization.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task OrganizationCreationReplaysTheSameResponseForAnIdempotencyKey()
    {
        var token = TestTokens.CreateAccessToken();
        var key = Guid.NewGuid().ToString("N");
        using var firstRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            token,
            new { name = "Contoso Retry" });
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var firstResponse = await client.SendAsync(firstRequest);
        var firstOrganization = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();

        using var retryRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            token,
            new { name = "Contoso Retry" });
        retryRequest.Headers.Add("Idempotency-Key", key);
        using var retryResponse = await client.SendAsync(retryRequest);
        var retriedOrganization = await retryResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        Assert.Equal(
            firstOrganization.GetProperty("id").GetGuid(),
            retriedOrganization.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task OrganizationCreationRejectsAnIdempotencyKeyReusedWithAnotherBody()
    {
        var token = TestTokens.CreateAccessToken();
        var key = Guid.NewGuid().ToString("N");
        using var firstRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            token,
            new { name = "Contoso First" });
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var firstResponse = await client.SendAsync(firstRequest);

        using var conflictingRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            token,
            new { name = "Contoso Second" });
        conflictingRequest.Headers.Add("Idempotency-Key", key);
        using var conflictingResponse = await client.SendAsync(conflictingRequest);
        var problem = await conflictingResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflictingResponse.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task OrganizationCreationUsesSeparateIdempotencyScopesForDifferentUsers()
    {
        var key = Guid.NewGuid().ToString("N");
        var firstToken = TestTokens.CreateAccessToken(objectId: "aaaaaaaa-1111-4111-8111-111111111111");
        var secondToken = TestTokens.CreateAccessToken(objectId: "bbbbbbbb-1111-4111-8111-111111111111");
        using var firstRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            firstToken,
            new { name = "Contoso First User" });
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var firstResponse = await client.SendAsync(firstRequest);
        var firstOrganization = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();

        using var secondRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/organizations",
            secondToken,
            new { name = "Contoso Second User" });
        secondRequest.Headers.Add("Idempotency-Key", key);
        using var secondResponse = await client.SendAsync(secondRequest);
        var secondOrganization = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotEqual(
            firstOrganization.GetProperty("id").GetGuid(),
            secondOrganization.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ConcurrentFirstRequestsForAnIdentityCreateOneProfile()
    {
        var objectId = Guid.NewGuid();
        var token = TestTokens.CreateAccessToken(
            objectId: objectId.ToString("D"),
            displayName: "Concurrent Alice");

        var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
        {
            using var request = CreateRequest(HttpMethod.Get, "/v1/organizations", token);
            return await client.SendAsync(request);
        }));

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("[]", await response.Content.ReadAsStringAsync());
        }
        foreach (var response in responses)
        {
            response.Dispose();
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        Assert.Equal(1, await dbContext.UserProfiles.CountAsync(profile => profile.ObjectId == objectId));
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
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

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

    [Fact]
    public async Task OrganizationMemberCanListOnlyMembersOfTheirOrganization()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-5555-4555-8555-555555555555",
            displayName: "Alice Martin");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Directory");

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/members",
            ownerToken);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(members);
        var owner = Assert.Single(members);
        Assert.Equal("Alice Martin", owner.GetProperty("displayName").GetString());
        Assert.Contains(
            "Administrator",
            owner.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task OrganizationAdministratorCanChangeMemberRoles()
    {
        const string ownerObjectId = "99999999-6666-4666-8666-666666666666";
        const string memberObjectId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Roles");
        var memberProfileId = await AddMemberAsync(
            organizationId,
            memberObjectId,
            memberToken,
            [OrganizationRole.Member]);

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/members/{memberProfileId:D}/roles",
            ownerToken,
            new { roles = ConnectionManagerMemberRoles });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedMember = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            "ConnectionManager",
            updatedMember.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task OrganizationMemberCannotChangeAnotherMembersRoles()
    {
        const string ownerObjectId = "99999999-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        const string memberObjectId = "bbbbbbbb-cccc-4ccc-8ccc-cccccccccccc";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Read Only Members");
        var memberProfileId = await AddMemberAsync(
            organizationId,
            memberObjectId,
            memberToken,
            [OrganizationRole.Member]);

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/members/{memberProfileId:D}/roles",
            memberToken,
            new { roles = ConnectionManagerMemberRoles });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdministratorCanRemoveAnOrganizationMember()
    {
        const string ownerObjectId = "99999999-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        const string memberObjectId = "bbbbbbbb-dddd-4ddd-8ddd-dddddddddddd";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Remove Member");
        var memberProfileId = await AddMemberAsync(
            organizationId,
            memberObjectId,
            memberToken,
            [OrganizationRole.Member]);

        using var request = CreateRequest(
            HttpMethod.Delete,
            $"/v1/organizations/{organizationId:D}/members/{memberProfileId:D}",
            ownerToken);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task NonAdministratorCanLeaveWhileAnAdministratorRemains()
    {
        const string ownerObjectId = "99999999-cccc-4ccc-8ccc-cccccccccccc";
        const string memberObjectId = "bbbbbbbb-eeee-4eee-8eee-eeeeeeeeeeee";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Leave Member");
        _ = await AddMemberAsync(organizationId, memberObjectId, memberToken, [OrganizationRole.Member]);

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/organizations/{organizationId:D}/leave",
            memberToken);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LastOrganizationAdministratorCannotLeave()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-7777-4777-8777-777777777777",
            displayName: "Alice Martin");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Last Admin");

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/organizations/{organizationId:D}/leave",
            ownerToken);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RG-ORG-05", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LastOrganizationAdministratorCannotBeDemoted()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-9999-4999-8999-999999999999",
            displayName: "Alice Martin");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Demotion");
        var administratorProfileId = await GetProfileIdAsync("99999999-9999-4999-8999-999999999999");

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/members/{administratorProfileId:D}/roles",
            ownerToken,
            new { roles = ConnectionManagerOnly });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RG-ORG-05", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdministratorCanUpdateAccessRestrictions()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-8888-4888-8888-888888888888",
            displayName: "Alice Martin");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Restrictions");

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/access-restrictions",
            ownerToken,
            new
            {
                allowedEmailDomains = ContosoEmailDomains,
                allowedTenantIds = ContosoTenantIds
            });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedRestrictions = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "contoso.example",
            Assert.Single(updatedRestrictions.GetProperty("allowedEmailDomains").EnumerateArray()).GetString());
        Assert.Equal(
            Guid.Parse(ContosoTenantIds[0]),
            Assert.Single(updatedRestrictions.GetProperty("allowedTenantIds").EnumerateArray()).GetGuid());
    }

    [Fact]
    public async Task MemberOutsideAccessRestrictionsIsMarkedInOrganizationMemberList()
    {
        const string ownerObjectId = "99999999-dddd-4ddd-8ddd-dddddddddddd";
        const string memberObjectId = "dddddddd-dddd-4ddd-8ddd-eeeeeeeeeeee";
        const string otherTenantId = "22222222-2222-2222-2222-222222222222";
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(
            tenantId: otherTenantId,
            objectId: memberObjectId,
            displayName: "David Moreau",
            email: "david@fabrikam.example");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Restrictions Status");
        _ = await AddMemberAsync(organizationId, memberObjectId, memberToken, [OrganizationRole.Member]);

        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/access-restrictions",
            ownerToken,
            new { allowedEmailDomains = ContosoEmailDomains, allowedTenantIds = ContosoTenantIds });
        updateRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        updateRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var updateResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var listRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/members",
            ownerToken);
        listRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var members = await listResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(members);
        var outOfRestrictionMember = Assert.Single(
            members,
            member => member.GetProperty("displayName").GetString() == "David Moreau");
        Assert.True(outOfRestrictionMember.GetProperty("isOutsideAccessRestrictions").GetBoolean());
    }

    private async Task<Guid> CreateOrganizationAsync(string token, string name)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v1/organizations", token, new { name });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var organization = await response.Content.ReadFromJsonAsync<JsonElement>();
        return organization.GetProperty("id").GetGuid();
    }

    private async Task<Guid> AddMemberAsync(
        Guid organizationId,
        string memberObjectId,
        string memberToken,
        OrganizationRole[] roles)
    {
        using var meRequest = CreateRequest(HttpMethod.Get, "/v1/me", memberToken);
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var member = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(memberObjectId));
        var organization = await dbContext.Organizations
            .Include(candidate => candidate.Members)
            .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));
        Assert.False(organization.AddMember(member.Id, roles).IsError);
        await dbContext.SaveChangesAsync();
        return member.Id.Value;
    }

    private async Task<Guid> GetProfileIdAsync(string objectId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(candidate => candidate.ObjectId == Guid.Parse(objectId));
        return profile.Id.Value;
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
