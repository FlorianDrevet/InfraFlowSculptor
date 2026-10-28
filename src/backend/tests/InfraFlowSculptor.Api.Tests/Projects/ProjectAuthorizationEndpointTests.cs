using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Projects;

public sealed class ProjectAuthorizationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public void ProjectEndpointsRequireTheOrganizationsFeature()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var endpoint = Assert.Single(endpoints, candidate =>
            candidate.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "GetMyProjectPermissions");

        Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<RequiredFeature>(),
            requirement => requirement.Key == Features.Organizations);
    }

    [Fact]
    public async Task SeededShopProjectGivesAliceAllPermissionsAndChloeProjectRead()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);

        const string tenantId = "11111111-1111-1111-1111-111111111111";
        var aliceToken = TestTokens.CreateAccessToken(
            tenantId: tenantId,
            objectId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            displayName: "Alice Martin",
            email: "alice@contoso.example");
        var chloeToken = TestTokens.CreateAccessToken(
            tenantId: tenantId,
            objectId: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
            displayName: "Chloé Petit",
            email: "chloe@contoso.example");
        var organizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");

        var alicePermissions = await GetMyPermissionsAsync(
            DemoDataSeeder.ShopProjectId.Value,
            organizationId,
            aliceToken);
        var aliceKeys = alicePermissions.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Equal(Permission.All.Order(StringComparer.Ordinal), aliceKeys.Order(StringComparer.Ordinal));

        var chloePermissions = await GetMyPermissionsAsync(
            DemoDataSeeder.ShopProjectId.Value,
            organizationId,
            chloeToken);
        var chloeKeys = chloePermissions.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Single(chloeKeys);
        Assert.Equal(Permission.ProjectRead, chloeKeys[0]);
    }

    [Fact]
    public async Task OwnerReaderAndMemberPermissionsMatchTheirProjectRoles()
    {
        var scenario = await CreateProjectScenarioAsync();

        var ownerPermissions = await GetMyPermissionsAsync(scenario.ProjectId, scenario.OrganizationId, scenario.OwnerToken);
        var ownerKeys = ownerPermissions.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Equal(15, ownerKeys.Length);
        Assert.Contains("project.administer", ownerKeys);
        Assert.Contains("members.manage", ownerKeys);

        var readerPermissions = await GetMyPermissionsAsync(
            scenario.ProjectId,
            scenario.OrganizationId,
            scenario.ReaderToken);
        var readerKeys = readerPermissions.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Single(readerKeys);
        Assert.Equal("project.read", readerKeys[0]);

        using var contributorRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/permissions",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var contributorResponse = await client.SendAsync(contributorRequest);
        Assert.Equal(HttpStatusCode.OK, contributorResponse.StatusCode);
        var contributorPermissions = await contributorResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            contributorPermissions.GetProperty("permissions").EnumerateArray(),
            permission => permission.GetString() == "model.edit");
    }

    [Fact]
    public async Task OrganizationMemberWithoutProjectRoleReceivesNotFound()
    {
        var scenario = await CreateProjectScenarioAsync(includeUnassignedMember: true);

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{scenario.ProjectId:D}/permissions/me",
            scenario.UnassignedToken!,
            scenario.OrganizationId);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemovingOrganizationMemberAlsoRemovesTheirProjectRoles()
    {
        var scenario = await CreateProjectScenarioAsync();

        using var request = CreateRequest(
            HttpMethod.Delete,
            $"/v1/organizations/{scenario.OrganizationId:D}/members/{scenario.ContributorId:D}",
            scenario.OwnerToken,
            scenario.OrganizationId);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var retainedAssignments = await dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.Id == new ProjectId(scenario.ProjectId))
            .SelectMany(project => project.RoleAssignments)
            .AnyAsync(assignment => assignment.Principal == new UserProfileId(scenario.ContributorId));

        Assert.False(retainedAssignments);

        using var auditRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{scenario.OrganizationId:D}/audit",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var auditResponse = await client.SendAsync(auditRequest);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditEvents = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "project.role.removed"
            && auditEvent.GetProperty("projectId").GetGuid() == scenario.ProjectId);
    }

    [Fact]
    public async Task LeavingOrganizationAlsoRemovesProjectRoles()
    {
        var scenario = await CreateProjectScenarioAsync();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/organizations/{scenario.OrganizationId:D}/leave",
            scenario.ContributorToken,
            scenario.OrganizationId);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var retainedAssignments = await dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.Id == new ProjectId(scenario.ProjectId))
            .SelectMany(project => project.RoleAssignments)
            .AnyAsync(assignment => assignment.Principal == new UserProfileId(scenario.ContributorId));

        Assert.False(retainedAssignments);

        using var auditRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{scenario.OrganizationId:D}/audit",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var auditResponse = await client.SendAsync(auditRequest);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditEvents = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "project.role.removed"
            && auditEvent.GetProperty("projectId").GetGuid() == scenario.ProjectId);
    }

    [Fact]
    public async Task TakingOwnershipHidesProjectsFromUnassignedMembersAndForbidsNonAdministratorsWithAccess()
    {
        var scenario = await CreateProjectScenarioAsync(includeUnassignedMember: true);

        using (var readerRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/ownership",
                   scenario.ReaderToken,
                   scenario.OrganizationId,
                   new { expectedVersion = 3 }))
        {
            readerRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var readerResponse = await client.SendAsync(readerRequest);
            Assert.Equal(HttpStatusCode.Forbidden, readerResponse.StatusCode);
        }

        using var unassignedRequest = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{scenario.ProjectId:D}/ownership",
            scenario.UnassignedToken!,
            scenario.OrganizationId,
            new { expectedVersion = 3 });
        unassignedRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var unassignedResponse = await client.SendAsync(unassignedRequest);
        Assert.Equal(HttpStatusCode.NotFound, unassignedResponse.StatusCode);
    }

    [Fact]
    public async Task OwnerCanAssignAndRemoveRolesAndAReaderCannotAssignThem()
    {
        var scenario = await CreateProjectScenarioAsync();
        var permissions = await GetMyPermissionsAsync(scenario.ProjectId, scenario.OrganizationId, scenario.OwnerToken);
        var initialVersion = permissions.GetProperty("version").GetInt32();

        using (var readerRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles",
                   scenario.ReaderToken,
                   scenario.OrganizationId,
                   new { role = "Auditor", scope = "Project", expectedVersion = initialVersion }))
        {
            readerRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var readerResponse = await client.SendAsync(readerRequest);
            Assert.Equal(HttpStatusCode.Forbidden, readerResponse.StatusCode);
            var problem = await readerResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PROJECT_PERMISSION_REQUIRED", problem.GetProperty("code").GetString());
            Assert.Equal("members.manage", problem.GetProperty("permission").GetString());
        }

        int assignedVersion;
        using (var assignRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles",
                   scenario.OwnerToken,
                   scenario.OrganizationId,
                   new { role = "Auditor", scope = "Project", expectedVersion = initialVersion }))
        {
            assignRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var assignResponse = await client.SendAsync(assignRequest);
            Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
            var assigned = await assignResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auditor", assigned.GetProperty("role").GetString());
            assignedVersion = assigned.GetProperty("version").GetInt32();
        }

        using (var removeRequest = CreateRequest(
                   HttpMethod.Delete,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles/Auditor?scope=Project&expectedVersion={assignedVersion}",
                   scenario.OwnerToken,
                   scenario.OrganizationId))
        {
            removeRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var removeResponse = await client.SendAsync(removeRequest);
            Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        }

        using var auditRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{scenario.OrganizationId:D}/audit",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var auditResponse = await client.SendAsync(auditRequest);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditEvents = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "project.role.assigned");
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "project.role.removed");
    }

    [Fact]
    public async Task ProjectAdministratorCannotRemoveAnotherMembersOwnerRole()
    {
        var scenario = await CreateProjectScenarioAsync();
        var permissions = await GetMyPermissionsAsync(scenario.ProjectId, scenario.OrganizationId, scenario.OwnerToken);
        var projectVersion = permissions.GetProperty("version").GetInt32();

        using (var ownerAssignmentRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ReaderId:D}/roles",
                   scenario.OwnerToken,
                   scenario.OrganizationId,
                   new { role = "Owner", scope = "Project", expectedVersion = projectVersion }))
        {
            ownerAssignmentRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var ownerAssignmentResponse = await client.SendAsync(ownerAssignmentRequest);
            Assert.Equal(HttpStatusCode.OK, ownerAssignmentResponse.StatusCode);
            projectVersion = (await ownerAssignmentResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt32();
        }

        using (var administratorAssignmentRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles",
                   scenario.OwnerToken,
                   scenario.OrganizationId,
                   new { role = "ProjectAdministrator", scope = "Project", expectedVersion = projectVersion }))
        {
            administratorAssignmentRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var administratorAssignmentResponse = await client.SendAsync(administratorAssignmentRequest);
            Assert.Equal(HttpStatusCode.OK, administratorAssignmentResponse.StatusCode);
            projectVersion = (await administratorAssignmentResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt32();
        }

        using var removeOwnerRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ReaderId:D}/roles/Owner?scope=Project&expectedVersion={projectVersion}",
            scenario.ContributorToken,
            scenario.OrganizationId);
        removeOwnerRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var removeOwnerResponse = await client.SendAsync(removeOwnerRequest);

        Assert.Equal(HttpStatusCode.Forbidden, removeOwnerResponse.StatusCode);
        var problem = await removeOwnerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RG-ORG-23", problem.GetProperty("code").GetString());
        Assert.Equal(Permission.ProjectAdminister, problem.GetProperty("permission").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var retainsOwnerRole = await dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.Id == new ProjectId(scenario.ProjectId))
            .SelectMany(project => project.RoleAssignments)
            .AnyAsync(assignment => assignment.Principal == new UserProfileId(scenario.ReaderId)
                && assignment.Role == ProjectRole.Owner);
        Assert.True(retainsOwnerRole);
    }

    [Fact]
    public async Task ReaderCannotRemoveRolesWithoutMembersManagePermission()
    {
        var scenario = await CreateProjectScenarioAsync();
        var ownerPermissions = await GetMyPermissionsAsync(
            scenario.ProjectId,
            scenario.OrganizationId,
            scenario.OwnerToken);
        var projectVersion = ownerPermissions.GetProperty("version").GetInt32();

        using (var assignAuditorRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles",
                   scenario.OwnerToken,
                   scenario.OrganizationId,
                   new { role = "Auditor", scope = "Project", expectedVersion = projectVersion }))
        {
            assignAuditorRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var assignAuditorResponse = await client.SendAsync(assignAuditorRequest);
            Assert.Equal(HttpStatusCode.OK, assignAuditorResponse.StatusCode);
            projectVersion = (await assignAuditorResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt32();
        }

        using var removeAuditorRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles/Auditor?scope=Project&expectedVersion={projectVersion}",
            scenario.ReaderToken,
            scenario.OrganizationId);
        removeAuditorRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var removeAuditorResponse = await client.SendAsync(removeAuditorRequest);

        Assert.Equal(HttpStatusCode.Forbidden, removeAuditorResponse.StatusCode);
        var problem = await removeAuditorResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PROJECT_PERMISSION_REQUIRED", problem.GetProperty("code").GetString());
        Assert.Equal(Permission.MembersManage, problem.GetProperty("permission").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var retainsAuditorRole = await dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.Id == new ProjectId(scenario.ProjectId))
            .SelectMany(project => project.RoleAssignments)
            .AnyAsync(assignment => assignment.Principal == new UserProfileId(scenario.ContributorId)
                && assignment.Role == ProjectRole.Auditor);
        Assert.True(retainsAuditorRole);
    }

    [Fact]
    public async Task AssigningProjectRoleAdvancesOrganizationAccessVersion()
    {
        var scenario = await CreateProjectScenarioAsync();
        var projectPermissions = await GetMyPermissionsAsync(
            scenario.ProjectId,
            scenario.OrganizationId,
            scenario.OwnerToken);
        var projectVersion = projectPermissions.GetProperty("version").GetInt32();
        var organizationVersion = await GetOrganizationVersionAsync(scenario.OrganizationId);

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{scenario.ProjectId:D}/members/{scenario.ContributorId:D}/roles",
            scenario.OwnerToken,
            scenario.OrganizationId,
            new { role = "Auditor", scope = "Project", expectedVersion = projectVersion });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(organizationVersion + 1, await GetOrganizationVersionAsync(scenario.OrganizationId));
    }

    [Fact]
    public async Task OrganizationAdministratorCanTakeOwnershipAndTheActionIsAudited()
    {
        var scenario = await CreateProjectScenarioAsync(includeOrganizationAdministrator: true);
        var permissions = await GetMyPermissionsAsync(
            scenario.ProjectId,
            scenario.OrganizationId,
            scenario.OrganizationAdministratorToken!);
        var administratorPermissions = permissions.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Single(administratorPermissions);
        Assert.Equal("project.read", administratorPermissions[0]);
        var initialVersion = permissions.GetProperty("version").GetInt32();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{scenario.ProjectId:D}/ownership",
            scenario.OrganizationAdministratorToken!,
            scenario.OrganizationId,
            new { expectedVersion = initialVersion });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Owner", assignment.GetProperty("role").GetString());

        using var auditRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/organizations/{scenario.OrganizationId:D}/audit",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var auditResponse = await client.SendAsync(auditRequest);
        var auditEvents = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.GetProperty("action").GetString() == "project.ownership.taken");
    }

    private async Task<ProjectScenario> CreateProjectScenarioAsync(
        bool includeUnassignedMember = false,
        bool includeOrganizationAdministrator = false)
    {
        var owner = CreateUserToken("Alice", "alice");
        var contributor = CreateUserToken("Bob", "bob");
        var reader = CreateUserToken("Chloé", "chloe");
        var unassigned = includeUnassignedMember ? CreateUserToken("Nina", "nina") : null;
        var organizationAdministrator = includeOrganizationAdministrator
            ? CreateUserToken("Nina", "nina-admin")
            : null;
        var organizationId = await CreateOrganizationAsync(owner);
        await SynchronizeProfileAsync(contributor.Token);
        await SynchronizeProfileAsync(reader.Token);
        if (unassigned is not null)
        {
            await SynchronizeProfileAsync(unassigned.Token);
        }

        if (organizationAdministrator is not null)
        {
            await SynchronizeProfileAsync(organizationAdministrator.Token);
        }

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var ownerId = await GetProfileIdAsync(dbContext, owner.ObjectId);
        var contributorId = await GetProfileIdAsync(dbContext, contributor.ObjectId);
        var readerId = await GetProfileIdAsync(dbContext, reader.ObjectId);
        var organization = await dbContext.Organizations
            .Include(candidate => candidate.Members)
            .SingleAsync(candidate => candidate.Id == new OrganizationId(organizationId));

        Assert.False(organization.AddMember(contributorId, [OrganizationRole.Member]).IsError);
        Assert.False(organization.AddMember(readerId, [OrganizationRole.Member]).IsError);

        UserProfileId? unassignedId = null;
        if (unassigned is not null)
        {
            unassignedId = await GetProfileIdAsync(dbContext, unassigned.ObjectId);
            Assert.False(organization.AddMember(unassignedId.Value, [OrganizationRole.Member]).IsError);
        }

        UserProfileId? organizationAdministratorId = null;
        if (organizationAdministrator is not null)
        {
            organizationAdministratorId = await GetProfileIdAsync(dbContext, organizationAdministrator.ObjectId);
            Assert.False(organization.AddMember(
                organizationAdministratorId.Value,
                [OrganizationRole.Administrator]).IsError);
        }

        var projectId = new ProjectId(Guid.NewGuid());
        var project = ProjectAggregate.Create(projectId, new OrganizationId(organizationId), "Shop Project", "shop", ownerId).Value;
        Assert.False(project.AssignRole(contributorId, ProjectRole.Contributor).IsError);
        Assert.False(project.AssignRole(readerId, ProjectRole.Reader).IsError);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        return new ProjectScenario(
            organizationId,
            projectId.Value,
            owner.Token,
            contributor.Token,
            reader.Token,
            contributorId.Value,
            readerId.Value,
            unassigned?.Token,
            organizationAdministrator?.Token);
    }

    private async Task<Guid> CreateOrganizationAsync(UserToken owner)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(new { name = $"Contoso {Guid.NewGuid():N}" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<int> GetOrganizationVersionAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        return await dbContext.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Id == new OrganizationId(organizationId))
            .Select(candidate => candidate.Version)
            .SingleAsync();
    }

    private async Task SynchronizeProfileAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<UserProfileId> GetProfileIdAsync(IfsDbContext dbContext, Guid objectId) =>
        await dbContext.UserProfiles
            .Where(profile => profile.ObjectId == objectId)
            .Select(profile => profile.Id)
            .SingleAsync();

    private async Task<JsonElement> GetMyPermissionsAsync(Guid projectId, Guid organizationId, string token)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{projectId:D}/permissions/me",
            token,
            organizationId);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string token,
        Guid organizationId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Ifs-Organization", organizationId.ToString("D"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static UserToken CreateUserToken(string displayName, string alias)
    {
        var objectId = Guid.NewGuid();
        return new UserToken(
            objectId,
            TestTokens.CreateAccessToken(
                objectId: objectId.ToString("D"),
                displayName: displayName,
                email: $"{alias}-{Guid.NewGuid():N}@contoso.example"));
    }

    private sealed record UserToken(Guid ObjectId, string Token);

    private sealed record ProjectScenario(
        Guid OrganizationId,
        Guid ProjectId,
        string OwnerToken,
        string ContributorToken,
        string ReaderToken,
        Guid ContributorId,
        Guid ReaderId,
        string? UnassignedToken,
        string? OrganizationAdministratorToken);
}
