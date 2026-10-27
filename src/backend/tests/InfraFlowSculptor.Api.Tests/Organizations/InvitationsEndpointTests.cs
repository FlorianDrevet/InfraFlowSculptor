using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Organizations;

public sealed class InvitationsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid ContosoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task InvitationStoresOnlyTheTokenHashAndRejectsUnverifiedOrMismatchedAccounts()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-0101-4101-8101-000000000001",
            displayName: "Alice Martin");
        var ninaToken = TestTokens.CreateAccessToken(
            objectId: "ffffffff-ffff-4fff-8fff-ffffffffffff",
            displayName: "Nina Moreau",
            email: "nina@contoso.example",
            emailVerified: false);
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Invitations");
        await SynchronizeProfileAsync(ninaToken);

        var invited = await InviteAsync(
            organizationId,
            ownerToken,
            "nina@contoso.example",
            [OrganizationRole.Member]);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var response = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = response.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var invitation = await dbContext.Invitations.IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == new InvitationId(invitationId));
            Assert.Equal(
                InvitationAggregate.ComputeTokenHash(token),
                invitation.TokenHash);
            Assert.DoesNotContain(token, invitation.TokenHash, StringComparison.Ordinal);
        }

        using var acceptRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", ninaToken);
        using var unverifiedResponse = await client.SendAsync(acceptRequest);
        var problem = await unverifiedResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, unverifiedResponse.StatusCode);
        Assert.Equal("INVITATION_EMAIL_NOT_VERIFIED", problem.GetProperty("code").GetString());

        var otherToken = TestTokens.CreateAccessToken(
            objectId: "22222222-0101-4101-8101-000000000002",
            displayName: "Other user",
            email: "other@contoso.example");
        await SynchronizeProfileAsync(otherToken);
        using var mismatchRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", otherToken);
        using var mismatchResponse = await client.SendAsync(mismatchRequest);
        var mismatchProblem = await mismatchResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, mismatchResponse.StatusCode);
        Assert.Equal("INVITATION_EMAIL_MISMATCH", mismatchProblem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvitationPreviewReturnsOnlyLandingDetailsAndCurrentAccountEligibility()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-0104-4104-8104-000000000004",
            displayName: "Alice Martin");
        var ninaToken = TestTokens.CreateAccessToken(
            objectId: "ffffffff-ffff-4fff-8fff-ffffffffffff",
            displayName: "Nina Moreau",
            email: "nina@contoso.example",
            emailVerified: false);
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Preview");
        await SynchronizeProfileAsync(ninaToken);
        var invited = await InviteAsync(organizationId, ownerToken, "nina@contoso.example", [OrganizationRole.Member]);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = invitation.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        using var previewRequest = CreateRequest(HttpMethod.Get, $"/v1/invitations/{token}", ninaToken);
        using var previewResponse = await client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("Contoso Preview", preview.GetProperty("organizationName").GetString());
        Assert.Equal("nina@contoso.example", preview.GetProperty("email").GetString());
        Assert.Equal("Pending", preview.GetProperty("status").GetString());
        Assert.False(preview.GetProperty("emailVerified").GetBoolean());
        Assert.False(preview.GetProperty("emailMatches").GetBoolean());
        Assert.True(preview.GetProperty("tenantAllowed").GetBoolean());
        Assert.False(preview.TryGetProperty("token", out _));
        Assert.False(preview.TryGetProperty("tokenHash", out _));
    }

    [Fact]
    public async Task TenantRestrictionRejectsPersonalAccountAndAcceptsItAfterRestrictionIsRemoved()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-0102-4102-8102-000000000002",
            displayName: "Alice Martin");
        var emmaToken = TestTokens.CreateAccessToken(
            tenantId: "9188040d-6c67-4c5b-b112-36a304b66dad",
            objectId: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
            displayName: "Emma Laurent",
            email: "emma@outlook.example");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Tenant Gate");
        await SynchronizeProfileAsync(emmaToken);
        await SetRestrictionsAsync(organizationId, ownerToken, [ContosoTenantId]);

        var rejected = await InviteAsync(organizationId, ownerToken, "emma@outlook.example", [OrganizationRole.Member]);
        var rejectedProblem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(
            rejected.StatusCode == HttpStatusCode.Forbidden,
            await rejected.Content.ReadAsStringAsync());
        Assert.Equal("RG-ORG-06", rejectedProblem.GetProperty("code").GetString());

        await SetRestrictionsAsync(organizationId, ownerToken, []);
        var invited = await InviteAsync(organizationId, ownerToken, "emma@outlook.example", [OrganizationRole.Member]);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = invitation.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        using var acceptRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", emmaToken);
        using var accepted = await client.SendAsync(acceptRequest);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var retryRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", emmaToken);
        using var retried = await client.SendAsync(retryRequest);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var storedInvitation = await dbContext.Invitations.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == new InvitationId(invitationId));
        var organization = await dbContext.Organizations.IgnoreQueryFilters()
            .Include(candidate => candidate.Members)
            .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));
        Assert.Equal(InvitationStatus.Accepted, storedInvitation.Status);
        var emma = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"));
        Assert.Contains(organization.Members, member => member.UserProfileId == emma.Id);
    }

    [Fact]
    public async Task RevokedInvitationCannotBeAccepted()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-0103-4103-8103-000000000003",
            displayName: "Alice Martin");
        var bobToken = TestTokens.CreateAccessToken(
            objectId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
            displayName: "Bob Bernard",
            email: "bob@contoso.example");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Revocations");
        await SynchronizeProfileAsync(bobToken);
        var invited = await InviteAsync(organizationId, ownerToken, "bob@contoso.example", [OrganizationRole.Member]);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = invitation.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        using var revokeRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/organizations/{organizationId:D}/invitations/{invitationId:D}",
            ownerToken);
        revokeRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        revokeRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var revoked = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var acceptRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", bobToken);
        using var response = await client.SendAsync(acceptRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVITATION_REVOKED", problem.GetProperty("code").GetString());
    }

    private async Task<Guid> CreateOrganizationAsync(string token, string name)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v1/organizations", token, new { name });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var organization = await response.Content.ReadFromJsonAsync<JsonElement>();
        return organization.GetProperty("id").GetGuid();
    }

    private async Task SynchronizeProfileAsync(string token)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v1/me", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> InviteAsync(
        Guid organizationId,
        string token,
        string email,
        OrganizationRole[] roles)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/organizations/{organizationId:D}/invitations",
            token,
            new { email, roles });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request);
    }

    private async Task SetRestrictionsAsync(Guid organizationId, string token, Guid[] tenantIds)
    {
        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/access-restrictions",
            token,
            new { allowedEmailDomains = Array.Empty<string>(), allowedTenantIds = tenantIds });
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> FindInvitationTokenAsync(Guid organizationId, Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var message = await dbContext.OutboxMessages.IgnoreQueryFilters()
            .Where(candidate => candidate.OrganizationId == new OrganizationId(organizationId)
                && candidate.Type == typeof(InvitationCreated).FullName)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstAsync();
        Assert.Equal(invitationId, message.Payload.GetProperty("InvitationId").GetGuid());
        return message.Payload.GetProperty("Token").GetString()!;
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
