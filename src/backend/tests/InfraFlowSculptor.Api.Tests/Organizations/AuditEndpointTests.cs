using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Organizations;

public sealed class AuditEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] MemberAndConnectionRoles = ["Member", "ConnectionManager"];
    private static readonly string[] MemberRoles = ["Member"];
    private static readonly string[] AllowedEmailDomains = ["contoso.example"];
    private static readonly Guid[] AllowedTenantIds = [Guid.Parse("11111111-1111-1111-1111-111111111111")];

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task OrganizationAdministratorCanReadAuditEventsWithBeforeAndAfterValues()
    {
        var token = TestTokens.CreateAccessToken(
            objectId: "99999999-eeee-4eee-8eee-eeeeeeeeeeee",
            displayName: "Alice Martin");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name = "Contoso Audit" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var organizationId = created.GetProperty("id").GetGuid();

        using var renameRequest = new HttpRequestMessage(HttpMethod.Put, $"/v1/organizations/{organizationId:D}")
        {
            Content = JsonContent.Create(new { name = "Contoso Audit Europe" })
        };
        renameRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        renameRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var renameResponse = await client.SendAsync(renameRequest);
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        using var auditRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/audit");
        auditRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        auditRequest.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        using var auditResponse = await client.SendAsync(auditRequest);

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditPage = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var events = auditPage.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, events.Length);

        var renameEvent = Assert.Single(events, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "organization.renamed");
        Assert.Equal("Alice Martin", renameEvent.GetProperty("actorDisplayName").GetString());
        Assert.Equal("Screen", renameEvent.GetProperty("via").GetString());
        var nameChange = Assert.Single(
            renameEvent.GetProperty("diff").GetProperty("changes").EnumerateArray(),
            change => change.GetProperty("name").GetString() == "name");
        Assert.Equal("Contoso Audit", nameChange.GetProperty("before").GetString());
        Assert.Equal("Contoso Audit Europe", nameChange.GetProperty("after").GetString());
    }

    [Fact]
    public async Task AuditRejectsPagesWhoseOffsetCannotBeRepresented()
    {
        var token = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Alice Martin");
        var organizationId = await CreateOrganizationAsync(token, "Contoso Audit Paging");

        using var request = CreateOrganizationRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/audit?page={int.MaxValue}&pageSize=100",
            token,
            organizationId);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AUDIT_PAGE_INVALID", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task OrganizationMemberWithoutAuditRoleIsDeniedWithThePermissionName()
    {
        var ownerObjectId = Guid.NewGuid().ToString("D");
        var memberObjectId = Guid.NewGuid().ToString("D");
        var auditorObjectId = Guid.NewGuid().ToString("D");
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var auditorToken = TestTokens.CreateAccessToken(objectId: auditorObjectId, displayName: "Chloe Martin");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Audit Access");
        await AddMemberAsync(organizationId, memberToken, memberObjectId, OrganizationRole.Member);
        await AddMemberAsync(organizationId, auditorToken, auditorObjectId, OrganizationRole.Auditor);

        using var request = CreateOrganizationRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/audit",
            memberToken,
            organizationId);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("audit.lire", problem.GetProperty("permission").GetString());

        using var auditorRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/audit",
            auditorToken,
            organizationId);
        using var auditorResponse = await client.SendAsync(auditorRequest);
        Assert.Equal(HttpStatusCode.OK, auditorResponse.StatusCode);
    }

    [Fact]
    public async Task RoleChangesAreAuditedAndInvitationAddressesAreStoredOutsideTheDiff()
    {
        var ownerObjectId = Guid.NewGuid().ToString("D");
        var memberObjectId = Guid.NewGuid().ToString("D");
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var memberToken = TestTokens.CreateAccessToken(objectId: memberObjectId, displayName: "Bob Bernard");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Audit Changes");
        var memberId = await AddMemberAsync(organizationId, memberToken, memberObjectId, OrganizationRole.Member);

        using var roleRequest = CreateOrganizationRequest(
            HttpMethod.Put,
            $"/v1/organizations/{organizationId:D}/members/{memberId:D}/roles",
            ownerToken,
            organizationId,
            new { roles = MemberAndConnectionRoles });
        roleRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var roleResponse = await client.SendAsync(roleRequest);
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);

        const string invitedEmail = "nina-audit@contoso.example";
        using var inviteRequest = CreateOrganizationRequest(
            HttpMethod.Post,
            $"/v1/organizations/{organizationId:D}/invitations",
            ownerToken,
            organizationId,
            new { email = invitedEmail, roles = MemberRoles });
        inviteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var inviteResponse = await client.SendAsync(inviteRequest);
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        using var auditRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            $"/v1/organizations/{organizationId:D}/audit",
            ownerToken,
            organizationId);
        using var auditResponse = await client.SendAsync(auditRequest);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var events = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();

        var roleEvent = Assert.Single(events, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "organization.member.roles_changed");
        var roleChange = Assert.Single(
            roleEvent.GetProperty("diff").GetProperty("changes").EnumerateArray(),
            change => change.GetProperty("name").GetString() == "roleValues");
        Assert.Equal("Member", roleChange.GetProperty("before").GetString());
        Assert.Equal("Member, ConnectionManager", roleChange.GetProperty("after").GetString());

        var invitationEvent = Assert.Single(events, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "organization.invitation.sent");
        var emailChange = Assert.Single(
            invitationEvent.GetProperty("diff").GetProperty("changes").EnumerateArray(),
            change => change.GetProperty("name").GetString() == "email");
        Assert.True(emailChange.GetProperty("isPersonalData").GetBoolean());
        Assert.Equal(invitedEmail, emailChange.GetProperty("after").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var invitationId = invitationEvent.GetProperty("id").GetGuid();
        var storedEvent = await dbContext.AuditEvents.IgnoreQueryFilters()
            .SingleAsync(auditEvent => auditEvent.Id == invitationId);
        Assert.DoesNotContain(invitedEmail, storedEvent.Diff, StringComparison.Ordinal);
        var storedPersonalData = await dbContext.AuditPersonalData.IgnoreQueryFilters()
            .SingleAsync(personalData => personalData.EventId == invitationId);
        Assert.Equal("email.after", storedPersonalData.Field);
        Assert.Equal(invitedEmail, storedPersonalData.Value);
    }

    [Fact]
    public async Task AcceptingInvitationWithDifferentEmailCasingLinksAuditPersonalDataToTheProfile()
    {
        var ownerToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Alice Martin");
        const string invitationEmail = "nina-casing@contoso.example";
        var inviteeObjectId = Guid.NewGuid();
        var inviteeToken = TestTokens.CreateAccessToken(
            objectId: inviteeObjectId.ToString("D"),
            displayName: "Nina Martin",
            email: invitationEmail.ToUpperInvariant());
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Audit Casing");
        await SynchronizeProfileAsync(inviteeToken);
        var matchingEmailDecoyToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Nina Martin Duplicate",
            email: invitationEmail);
        await SynchronizeProfileAsync(matchingEmailDecoyToken);

        using var inviteRequest = CreateOrganizationRequest(
            HttpMethod.Post,
            $"/v1/organizations/{organizationId:D}/invitations",
            ownerToken,
            organizationId,
            new { email = invitationEmail, roles = MemberRoles });
        inviteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var inviteResponse = await client.SendAsync(inviteRequest);
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invitationId = (await inviteResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var invitationToken = await ReadInvitationTokenAsync(organizationId, invitationId);

        using var acceptRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/invitations/{invitationToken}/accept");
        acceptRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", inviteeToken);
        acceptRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var acceptResponse = await client.SendAsync(acceptRequest);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var invitee = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == inviteeObjectId);
        var auditEvent = await dbContext.AuditEvents.IgnoreQueryFilters()
            .SingleAsync(auditEvent => auditEvent.OrganizationId == new OrganizationId(organizationId)
                && auditEvent.Action == AuditActions.InvitationSent);
        var personalData = await dbContext.AuditPersonalData.IgnoreQueryFilters()
            .SingleAsync(data => data.EventId == auditEvent.Id && data.Field == "email.after");
        Assert.Equal(invitationEmail, personalData.Value);
        Assert.Equal(invitee.Id, personalData.SubjectUserId);
    }

    [Fact]
    public async Task EveryExistingOrganizationCommandWritesExactlyOneAuditEvent()
    {
        var ownerObjectId = Guid.NewGuid().ToString("D");
        var bobObjectId = Guid.NewGuid().ToString("D");
        var chloeObjectId = Guid.NewGuid().ToString("D");
        var invitedObjectId = Guid.NewGuid().ToString("D");
        var ownerToken = TestTokens.CreateAccessToken(objectId: ownerObjectId, displayName: "Alice Martin");
        var bobToken = TestTokens.CreateAccessToken(
            objectId: bobObjectId,
            displayName: "Bob Bernard",
            email: "bob-audit@contoso.example");
        var chloeToken = TestTokens.CreateAccessToken(
            objectId: chloeObjectId,
            displayName: "Chloé Petit",
            email: "chloe-audit@contoso.example");
        var organizationId = await CreateOrganizationAsync(ownerToken, "Contoso Audit Commands");
        await AssertAuditCountAsync(organizationId, 1);

        using (var request = CreateOrganizationRequest(
                   HttpMethod.Put,
                   $"/v1/organizations/{organizationId:D}",
                   ownerToken,
                   organizationId,
                   new { name = "Contoso Audit Commands Updated" }))
        using (var response = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 2);
        var bobId = await AddMemberAsync(organizationId, bobToken, bobObjectId, OrganizationRole.Member);

        using (var request = CreateOrganizationRequest(
                   HttpMethod.Put,
                   $"/v1/organizations/{organizationId:D}/members/{bobId:D}/roles",
                   ownerToken,
                   organizationId,
                   new { roles = MemberAndConnectionRoles }))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 3);

        using (var request = CreateOrganizationRequest(
                   HttpMethod.Put,
                   $"/v1/organizations/{organizationId:D}/access-restrictions",
                   ownerToken,
                   organizationId,
                   new
                   {
                       allowedEmailDomains = AllowedEmailDomains,
                       allowedTenantIds = AllowedTenantIds
                   }))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 4);

        async Task<(Guid Id, string Token)> InviteAndReadTokenAsync(string email)
        {
            using var request = CreateOrganizationRequest(
                HttpMethod.Post,
                $"/v1/organizations/{organizationId:D}/invitations",
                ownerToken,
                organizationId,
                new { email, roles = MemberRoles });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                await response.Content.ReadAsStringAsync());
            var invitationId = (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            return (invitationId, await ReadInvitationTokenAsync(organizationId, invitationId));
        }

        var resendToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Resend User",
            email: "resend-audit@contoso.example");
        await SynchronizeProfileAsync(resendToken);
        var (resentInvitationId, _) = await InviteAndReadTokenAsync("resend-audit@contoso.example");
        await AssertAuditCountAsync(organizationId, 5);
        using (var request = CreateOrganizationRequest(
                   HttpMethod.Post,
                   $"/v1/organizations/{organizationId:D}/invitations/{resentInvitationId:D}/resend",
                   ownerToken,
                   organizationId))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 6);

        var invitedToken = TestTokens.CreateAccessToken(
            objectId: invitedObjectId,
            displayName: "Invited User",
            email: "accept-audit@contoso.example");
        await SynchronizeProfileAsync(invitedToken);
        var (_, invitationToken) = await InviteAndReadTokenAsync("accept-audit@contoso.example");
        await AssertAuditCountAsync(organizationId, 7);

        using (var request = new HttpRequestMessage(
                   HttpMethod.Post,
                   $"/v1/invitations/{invitationToken}/accept"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", invitedToken);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 8);

        var revokeToken = TestTokens.CreateAccessToken(
            objectId: Guid.NewGuid().ToString("D"),
            displayName: "Revoke User",
            email: "revoke-audit@contoso.example");
        await SynchronizeProfileAsync(revokeToken);
        var (revokedInvitationId, _) = await InviteAndReadTokenAsync("revoke-audit@contoso.example");
        await AssertAuditCountAsync(organizationId, 9);
        using (var request = CreateOrganizationRequest(
                   HttpMethod.Delete,
                   $"/v1/organizations/{organizationId:D}/invitations/{revokedInvitationId:D}",
                   ownerToken,
                   organizationId))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 10);

        using (var request = CreateOrganizationRequest(
                   HttpMethod.Delete,
                   $"/v1/organizations/{organizationId:D}/members/{bobId:D}",
                   ownerToken,
                   organizationId))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 11);
        _ = await AddMemberAsync(organizationId, chloeToken, chloeObjectId, OrganizationRole.Member);
        using (var request = CreateOrganizationRequest(
                   HttpMethod.Post,
                   $"/v1/organizations/{organizationId:D}/leave",
                   chloeToken,
                   organizationId))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await AssertAuditCountAsync(organizationId, 12);
    }

    private async Task<Guid> CreateOrganizationAsync(string token, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> AddMemberAsync(
        Guid organizationId,
        string memberToken,
        string memberObjectId,
        OrganizationRole role)
    {
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        using var profileResponse = await client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var member = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(memberObjectId));
        var organization = await dbContext.Organizations
            .Include(candidate => candidate.Members)
            .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));
        Assert.False(organization.AddMember(member.Id, [role]).IsError);
        await dbContext.SaveChangesAsync();
        return member.Id.Value;
    }

    private async Task AssertAuditCountAsync(Guid organizationId, long expectedCount)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var count = await dbContext.AuditEvents.IgnoreQueryFilters()
            .LongCountAsync(auditEvent => auditEvent.OrganizationId == new OrganizationId(organizationId));
        Assert.Equal(expectedCount, count);
    }

    private async Task SynchronizeProfileAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> ReadInvitationTokenAsync(Guid organizationId, Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var messages = await dbContext.OutboxMessages.IgnoreQueryFilters()
            .Where(message => message.OrganizationId == new OrganizationId(organizationId)
                && message.Type == typeof(InvitationCreated).FullName)
            .OrderByDescending(message => message.CreatedAt)
            .ToListAsync();
        var message = Assert.Single(messages, candidate =>
            candidate.Payload.GetProperty("InvitationId").GetGuid() == invitationId);
        return message.Payload.GetProperty("Token").GetString()
            ?? throw new InvalidOperationException("The invitation-created event had no token.");
    }

    private static HttpRequestMessage CreateOrganizationRequest(
        HttpMethod method,
        string path,
        string token,
        Guid organizationId,
        object? content = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }
}
