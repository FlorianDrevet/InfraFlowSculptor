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
    public async Task InvitationPreviewWithMalformedTokenReturnsNotFound()
    {
        var userToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Invitation Preview User");

        using var request = CreateRequest(HttpMethod.Get, "/v1/invitations/%20", userToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_FOUND", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AcceptInvitationWithMalformedTokenReturnsBadRequest()
    {
        var userToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Invitation Accept User");

        using var request = CreateRequest(HttpMethod.Post, "/v1/invitations/%20/accept", userToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        await SetRestrictionsAsync(organizationId, ownerToken, [], [ContosoTenantId]);

        var rejected = await InviteAsync(organizationId, ownerToken, "emma@outlook.example", [OrganizationRole.Member]);
        var rejectedProblem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(
            rejected.StatusCode == HttpStatusCode.Forbidden,
            await rejected.Content.ReadAsStringAsync());
        Assert.Equal("RG-ORG-06", rejectedProblem.GetProperty("code").GetString());

        await SetRestrictionsAsync(organizationId, ownerToken, [], []);
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
    public async Task TenantRestrictionRejectsRecipientWithoutKnownTenantProfile()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Invitation Owner");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Unknown Tenant");
        await SetRestrictionsAsync(organizationId, ownerToken, [], [ContosoTenantId]);

        var invited = await InviteAsync(
            organizationId,
            ownerToken,
            $"new-user-{Guid.NewGuid():N}@contoso.example",
            [OrganizationRole.Member]);

        Assert.Equal(HttpStatusCode.Forbidden, invited.StatusCode);
        var problem = await invited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RG-ORG-06", problem.GetProperty("code").GetString());
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

    [Fact]
    public async Task AcceptanceRechecksTheCurrentEmailDomainRestriction()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Invitation Owner");
        var email = $"restricted-{Guid.NewGuid():N}@contoso.example";
        var inviteeToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Restricted Invitee",
            email: email);
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Changed Domain Policy");
        await SynchronizeProfileAsync(inviteeToken);
        var invited = await InviteAsync(organizationId, ownerToken, email, [OrganizationRole.Member]);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = invitation.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        await SetRestrictionsAsync(organizationId, ownerToken, ["other.example"], []);
        using var acceptRequest = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", inviteeToken);
        using var response = await client.SendAsync(acceptRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RG-ORG-06", problem.GetProperty("code").GetString());
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var storedInvitation = await dbContext.Invitations.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == new InvitationId(invitationId));
        Assert.Equal(InvitationStatus.Pending, storedInvitation.Status);
    }

    [Fact]
    public async Task ConcurrentInvitationsForSameAddressCreateOnlyOnePendingInvitation()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: "99999999-0201-4201-8201-000000000001",
            displayName: "Alice Martin");
        var email = $"concurrent-{Guid.NewGuid():N}@contoso.example";
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Concurrent Invitations");
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => InviteAsync(organizationId, ownerToken, email, [OrganizationRole.Member])));

        try
        {
            var responseSummaries = await Task.WhenAll(responses.Select(async response =>
                $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.True(
                responses.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 7,
                string.Join(Environment.NewLine, responseSummaries));

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var pendingCount = await dbContext.Invitations.IgnoreQueryFilters()
                .CountAsync(invitation => invitation.OrganizationId == new OrganizationId(organizationId)
                    && invitation.Email == email
                    && invitation.Status == InvitationStatus.Pending);
            Assert.Equal(1, pendingCount);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task ConcurrentAcceptanceOfOneInvitationReturnsOnlyExpectedOutcomes()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Invitation Owner");
        var email = $"accept-{Guid.NewGuid():N}@contoso.example";
        var inviteeObjectId = Guid.NewGuid();
        var inviteeToken = TestTokens.CreateAccessToken(
            objectId: inviteeObjectId.ToString("D"),
            displayName: "Concurrent Invitee",
            email: email);
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Concurrent Acceptance");
        await SynchronizeProfileAsync(inviteeToken);
        var invited = await InviteAsync(
            organizationId,
            ownerToken,
            email,
            [OrganizationRole.Member]);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = invitation.GetProperty("id").GetGuid();
        var token = await FindInvitationTokenAsync(organizationId, invitationId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
        {
            var request = CreateRequest(HttpMethod.Post, $"/v1/invitations/{token}/accept", inviteeToken);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            return client.SendAsync(request);
        }));

        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            var responseDetails = await Task.WhenAll(responses.Select(async response =>
                $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));
            Assert.All(responses, response => Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                string.Join(Environment.NewLine, responseDetails)));

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var storedInvitation = await dbContext.Invitations.IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == new InvitationId(invitationId));
            var organization = await dbContext.Organizations.IgnoreQueryFilters()
                .Include(candidate => candidate.Members)
                .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));
            var invitee = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == inviteeObjectId);

            Assert.Equal(InvitationStatus.Accepted, storedInvitation.Status);
            Assert.Single(organization.Members, member => member.UserProfileId == invitee.Id);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
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

    private async Task SetRestrictionsAsync(
        Guid organizationId,
        string token,
        string[] emailDomains,
        Guid[] tenantIds)
    {
        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/access-restrictions",
            token,
            new { allowedEmailDomains = emailDomains, allowedTenantIds = tenantIds });
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
