using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Projects.Commands.CreateProject;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mediator;
using NSubstitute;

namespace InfraFlowSculptor.Api.Tests.Projects;

[SuppressMessage("Reliability", "CA2012", Justification = "NSubstitute uses ValueTask-returning calls as setup specifications.")]
public sealed class ProjectAuthorizationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] ProductionApprovers = ["alice@contoso.example"];
    private static readonly string[] ExpectedEnvironmentCodes = ["dev", "test", "prd"];
    private static readonly string[] ExpectedCodesAfterEnvironmentDeletion = ["dev", "prd"];
    private static readonly int[] ExpectedEnvironmentPositions = [1, 2, 3];

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
    public async Task ProjectSettingsCanBeUpdatedWhenExpectedVersionMatches()
    {
        var scenario = await CreateProjectScenarioAsync();
        var projectId = scenario.ProjectId;
        var expectedVersion = await GetProjectVersionAsync(projectId, scenario.OrganizationId, scenario.OwnerToken);

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{projectId:D}",
            scenario.OwnerToken,
            scenario.OrganizationId,
            new
            {
                name = "Boutique en ligne",
                code = "shop",
                description = "Catalogue actualisé",
                systemTagsEnabled = true,
                coordinatorUserProfileId = (Guid?)null,
                tags = new Dictionary<string, string>(),
                confirmImpact = true,
                expectedVersion
            });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var updated = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Boutique en ligne", updated.RootElement.GetProperty("name").GetString());
        Assert.Equal("Catalogue actualisé", updated.RootElement.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData(" second project ", "shop")]
    [InlineData("Unique Project", "SECOND")]
    public async Task ProjectSettingsRejectAnotherProjectsNameOrCodeIgnoringCase(string name, string code)
    {
        var scenario = await CreateProjectScenarioAsync();
        var otherProjectId = new ProjectId(Guid.NewGuid());
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(scenario.OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var sourceProject = await dbContext.Projects
                .Include(project => project.RoleAssignments)
                .SingleAsync(project => project.Id == new ProjectId(scenario.ProjectId));
            var ownerId = sourceProject.RoleAssignments
                .Single(assignment => assignment.Role == ProjectRole.Owner)
                .Principal;
            var otherProject = ProjectAggregate.Create(
                otherProjectId,
                new OrganizationId(scenario.OrganizationId),
                "Second Project",
                "second",
                ownerId).Value;
            dbContext.Projects.Add(otherProject);
            await dbContext.SaveChangesAsync();
        }

        var expectedVersion = await GetProjectVersionAsync(
            scenario.ProjectId,
            scenario.OrganizationId,
            scenario.OwnerToken);
        using var request = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{scenario.ProjectId:D}",
            scenario.OwnerToken,
            scenario.OrganizationId,
            new
            {
                name,
                code,
                description = "",
                systemTagsEnabled = true,
                coordinatorUserProfileId = (Guid?)null,
                tags = new Dictionary<string, string>(),
                confirmImpact = true,
                expectedVersion
            });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "PROJECT_NAME_OR_CODE_DUPLICATE",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectListCanIncludeDeletedProjectsForRestoration()
    {
        var scenario = await CreateProjectScenarioAsync();
        var version = await GetProjectVersionAsync(scenario.ProjectId, scenario.OrganizationId, scenario.OwnerToken);
        using var deleteRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{scenario.ProjectId:D}",
            scenario.OwnerToken,
            scenario.OrganizationId,
            new { nameConfirmation = "Shop Project", expectedVersion = version });
        deleteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.True(
            deleteResponse.StatusCode == HttpStatusCode.NoContent,
            await deleteResponse.Content.ReadAsStringAsync());

        using var listRequest = CreateRequest(
            HttpMethod.Get,
            "/v1/projects?includeDeleted=true",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var projects = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var deletedProject = projects.EnumerateArray()
            .Single(project => project.GetProperty("id").GetGuid() == scenario.ProjectId);

        Assert.NotEqual(JsonValueKind.Null, deletedProject.GetProperty("deletedAt").ValueKind);
    }

    [Fact]
    public async Task ProjectFavoritesAreStoredPerUserAndListedBeforeOtherProjects()
    {
        var scenario = await CreateProjectScenarioAsync();
        var alphabeticalProjectId = new ProjectId(Guid.NewGuid());
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(scenario.OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var sourceProject = await dbContext.Projects
                .Include(project => project.RoleAssignments)
                .SingleAsync(project => project.Id == new ProjectId(scenario.ProjectId));
            var ownerProfileId = sourceProject.RoleAssignments
                .Single(assignment => assignment.Role == ProjectRole.Owner)
                .Principal;
            var alphabeticalProject = ProjectAggregate.Create(
                alphabeticalProjectId,
                new OrganizationId(scenario.OrganizationId),
                "Aardvark project",
                "aardvark",
                ownerProfileId).Value;
            dbContext.Projects.Add(alphabeticalProject);
            await dbContext.SaveChangesAsync();
        }

        using var favoriteRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{scenario.ProjectId:D}/favorite",
            scenario.OwnerToken,
            scenario.OrganizationId);
        favoriteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var favoriteResponse = await client.SendAsync(favoriteRequest);
        Assert.True(
            favoriteResponse.StatusCode == HttpStatusCode.NoContent,
            await favoriteResponse.Content.ReadAsStringAsync());

        using var duplicateFavoriteRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{scenario.ProjectId:D}/favorite",
            scenario.OwnerToken,
            scenario.OrganizationId);
        duplicateFavoriteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var duplicateFavoriteResponse = await client.SendAsync(duplicateFavoriteRequest);
        Assert.Equal(HttpStatusCode.NoContent, duplicateFavoriteResponse.StatusCode);

        using var ownerListRequest = CreateRequest(
            HttpMethod.Get,
            "/v1/projects",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var ownerListResponse = await client.SendAsync(ownerListRequest);
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        using var ownerProjects = JsonDocument.Parse(await ownerListResponse.Content.ReadAsStringAsync());
        var ownerProjectList = ownerProjects.RootElement.EnumerateArray().ToArray();
        Assert.Equal(scenario.ProjectId, ownerProjectList[0].GetProperty("id").GetGuid());
        Assert.True(ownerProjectList[0].GetProperty("isFavorite").GetBoolean());

        using var readerListRequest = CreateRequest(
            HttpMethod.Get,
            "/v1/projects",
            scenario.ReaderToken,
            scenario.OrganizationId);
        using var readerListResponse = await client.SendAsync(readerListRequest);
        Assert.Equal(HttpStatusCode.OK, readerListResponse.StatusCode);
        using var readerProjects = JsonDocument.Parse(await readerListResponse.Content.ReadAsStringAsync());
        var readerShop = readerProjects.RootElement.EnumerateArray()
            .Single(project => project.GetProperty("id").GetGuid() == scenario.ProjectId);
        Assert.False(readerShop.GetProperty("isFavorite").GetBoolean());

        using var removeFavoriteRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{scenario.ProjectId:D}/favorite",
            scenario.OwnerToken,
            scenario.OrganizationId);
        removeFavoriteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var removeFavoriteResponse = await client.SendAsync(removeFavoriteRequest);
        Assert.Equal(HttpStatusCode.NoContent, removeFavoriteResponse.StatusCode);

        using var duplicateRemoveFavoriteRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{scenario.ProjectId:D}/favorite",
            scenario.OwnerToken,
            scenario.OrganizationId);
        duplicateRemoveFavoriteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var duplicateRemoveFavoriteResponse = await client.SendAsync(duplicateRemoveFavoriteRequest);
        Assert.Equal(HttpStatusCode.NoContent, duplicateRemoveFavoriteResponse.StatusCode);

        using var ownerListAfterRemovalRequest = CreateRequest(
            HttpMethod.Get,
            "/v1/projects",
            scenario.OwnerToken,
            scenario.OrganizationId);
        using var ownerListAfterRemovalResponse = await client.SendAsync(ownerListAfterRemovalRequest);
        Assert.Equal(HttpStatusCode.OK, ownerListAfterRemovalResponse.StatusCode);
        using var ownerProjectsAfterRemoval = JsonDocument.Parse(
            await ownerListAfterRemovalResponse.Content.ReadAsStringAsync());
        var ownerShopAfterRemoval = ownerProjectsAfterRemoval.RootElement.EnumerateArray()
            .Single(project => project.GetProperty("id").GetGuid() == scenario.ProjectId);
        Assert.False(ownerShopAfterRemoval.GetProperty("isFavorite").GetBoolean());
        Assert.Equal(
            alphabeticalProjectId.Value,
            ownerProjectsAfterRemoval.RootElement[0].GetProperty("id").GetGuid());
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
    public async Task ProjectEnvironmentChainRequiresProtectedApproversAndKeepsContinuousOrder()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);
        const string tenantId = "11111111-1111-1111-1111-111111111111";
        const string organizationId = "33333333-3333-4333-8333-333333333333";
        var token = TestTokens.CreateAccessToken(
            tenantId: tenantId,
            objectId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            displayName: "Alice Martin",
            email: "alice@contoso.example");
        var initialVersion = await GetProjectVersionAsync(DemoDataSeeder.ShopProjectId.Value, Guid.Parse(organizationId), token);

        using var devResponse = await AddEnvironmentAsync(
            token,
            Guid.Parse(organizationId),
            new { name = "Development", code = "dev", position = 1, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = initialVersion, deploymentConnection = "shop-dev-custom" });
        Assert.True(
            devResponse.StatusCode == HttpStatusCode.Created,
            await devResponse.Content.ReadAsStringAsync());

        using var staleAdd = await AddEnvironmentAsync(
            token,
            Guid.Parse(organizationId),
            new { name = "Stale", code = "stale", position = 2, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = initialVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleAdd.StatusCode);
        var afterDevelopmentVersion = await GetProjectVersionAsync(DemoDataSeeder.ShopProjectId.Value, Guid.Parse(organizationId), token);

        using var rejectedProduction = await AddEnvironmentAsync(
            token,
            Guid.Parse(organizationId),
            new { name = "Production", code = "prd", position = 2, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = true, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = afterDevelopmentVersion });
        Assert.Equal(HttpStatusCode.BadRequest, rejectedProduction.StatusCode);
        var rejection = await rejectedProduction.Content.ReadAsStringAsync();
        Assert.Contains("RG-ENV-APPROVER-REQUIRED", rejection, StringComparison.Ordinal);

        using var productionResponse = await AddEnvironmentAsync(
            token,
            Guid.Parse(organizationId),
            new { name = "Production", code = "prd", position = 2, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = true, approvers = ProductionApprovers, runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = afterDevelopmentVersion });
        Assert.Equal(HttpStatusCode.Created, productionResponse.StatusCode);

        var afterProductionVersion = await GetProjectVersionAsync(DemoDataSeeder.ShopProjectId.Value, Guid.Parse(organizationId), token);
        using var testResponse = await AddEnvironmentAsync(
            token,
            Guid.Parse(organizationId),
            new { name = "Test", code = "test", position = 2, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = afterProductionVersion });
        Assert.Equal(HttpStatusCode.Created, testResponse.StatusCode);

        using var listRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments",
            token,
            Guid.Parse(organizationId));
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var environments = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            ExpectedEnvironmentCodes,
            environments.EnumerateArray().Select(environment => environment.GetProperty("code").GetString()).ToArray());
        Assert.Equal(
            ExpectedEnvironmentPositions,
            environments.EnumerateArray().Select(environment => environment.GetProperty("position").GetInt32()).ToArray());
        var development = environments.EnumerateArray().Single(environment => environment.GetProperty("code").GetString() == "dev");
        var testing = environments.EnumerateArray().Single(environment => environment.GetProperty("code").GetString() == "test");
        Assert.Equal("shop-dev-custom", development.GetProperty("deploymentConnection").GetString());

        using (var publishScope = factory.Services.CreateScope())
        {
            var publishContext = publishScope.ServiceProvider.GetRequiredService<IfsDbContext>();
            publishScope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(Guid.Parse(organizationId)));
            var project = await publishContext.Projects
                .Include(candidate => candidate.Environments)
                .SingleAsync(candidate => candidate.Id == DemoDataSeeder.ShopProjectId);
            project.RecordPublication(DateTimeOffset.UtcNow);
            await publishContext.SaveChangesAsync();
        }

        var targetChangeVersion = await GetProjectVersionAsync(
            DemoDataSeeder.ShopProjectId.Value,
            Guid.Parse(organizationId),
            token);
        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments/{development.GetProperty("id").GetGuid():D}",
            token,
            Guid.Parse(organizationId),
            new
            {
                name = "Development",
                code = "dev",
                subscriptionId = development.GetProperty("subscriptionId").GetGuid(),
                region = "westeurope",
                isProtected = false,
                approvers = Array.Empty<string>(),
                runners = Array.Empty<string>(),
                description = "",
                tags = new Dictionary<string, string>(),
                confirmTargetImpact = false,
                expectedVersion = targetChangeVersion,
                deploymentConnection = "shop-dev-updated"
            });
        updateRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var updateResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        var impact = await updateResponse.Content.ReadAsStringAsync();
        Assert.Contains("affectedResources", impact, StringComparison.Ordinal);
        Assert.Contains("[]", impact, StringComparison.Ordinal);

        using var confirmTargetRequest = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments/{development.GetProperty("id").GetGuid():D}",
            token,
            Guid.Parse(organizationId),
            new
            {
                name = "Development",
                code = "dev",
                subscriptionId = development.GetProperty("subscriptionId").GetGuid(),
                region = "westeurope",
                isProtected = false,
                approvers = Array.Empty<string>(),
                runners = Array.Empty<string>(),
                description = "",
                tags = new Dictionary<string, string>(),
                confirmTargetImpact = true,
                expectedVersion = targetChangeVersion,
                deploymentConnection = "shop-dev-updated"
            });
        confirmTargetRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var confirmedResponse = await client.SendAsync(confirmTargetRequest);
        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);
        var updatedEnvironment = await confirmedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("shop-dev-updated", updatedEnvironment.GetProperty("deploymentConnection").GetString());
        Assert.Equal("westeurope", updatedEnvironment.GetProperty("region").GetString());

        var deleteVersion = await GetProjectVersionAsync(
            DemoDataSeeder.ShopProjectId.Value,
            Guid.Parse(organizationId),
            token);
        using var deletePreview = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments/{testing.GetProperty("id").GetGuid():D}",
            token,
            Guid.Parse(organizationId),
            new { confirmDeletion = false, expectedVersion = deleteVersion });
        deletePreview.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var deletePreviewResponse = await client.SendAsync(deletePreview);
        Assert.Equal(HttpStatusCode.Conflict, deletePreviewResponse.StatusCode);
        var affectedData = await deletePreviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("affectedData", affectedData, StringComparison.Ordinal);
        Assert.Contains("environmentId", affectedData, StringComparison.Ordinal);

        using var deleteRequest = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments/{testing.GetProperty("id").GetGuid():D}",
            token,
            Guid.Parse(organizationId),
            new { confirmDeletion = true, expectedVersion = deleteVersion });
        deleteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var reorderedRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments",
            token,
            Guid.Parse(organizationId));
        using var reorderedResponse = await client.SendAsync(reorderedRequest);
        var reordered = await reorderedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExpectedCodesAfterEnvironmentDeletion, reordered.EnumerateArray().Select(environment => environment.GetProperty("code").GetString()).ToArray());
    }

    [Fact]
    public async Task CreateProjectPinsPublishedCatalogAndEnforcesCaseInsensitiveNameAndCodeUniqueness()
    {
        var owner = CreateUserToken("Alice", "project-create");
        var organizationId = await CreateOrganizationAsync(owner);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var requestBody = new
        {
            name = "My Project",
            code = "myproject",
            tenantId,
            description = "Pilot project",
            defaultRunners = new[] { "ubuntu-latest" },
            defaultReviewers = new[] { "release-approvers" }
        };

        using var createRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/projects",
            owner.Token,
            organizationId,
            requestBody);
        createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("My Project", created.GetProperty("name").GetString());
        Assert.Equal("myproject", created.GetProperty("code").GetString());
        Assert.Equal(1, created.GetProperty("version").GetInt32());
        Assert.Equal(1, created.GetProperty("modelVersion").GetInt32());
        Assert.Equal("2026.10-pilot.1", created.GetProperty("catalogVersion").GetString());
        Assert.Equal("ubuntu-latest", created.GetProperty("defaultRunners")[0].GetString());
        Assert.Equal("release-approvers", created.GetProperty("defaultReviewers")[0].GetString());

        using var duplicateRequest = CreateRequest(
            HttpMethod.Post,
            "/v1/projects",
            owner.Token,
            organizationId,
            new { name = "MY PROJECT", code = "MYPROJECT", tenantId, description = "" });
        duplicateRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var duplicateResponse = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var listRequest = CreateRequest(HttpMethod.Get, "/v1/projects", owner.Token, organizationId);
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var projects = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(projects.EnumerateArray(), project =>
            project.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());

        using var getRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{created.GetProperty("id").GetGuid():D}",
            owner.Token,
            organizationId);
        using var getResponse = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var projectDetails = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(tenantId, projectDetails.GetProperty("tenantId").GetGuid());
        Assert.Equal("ubuntu-latest", projectDetails.GetProperty("defaultRunners")[0].GetString());
    }

    [Fact]
    public async Task ModelCommandDryRunDoesNotPersistDatabaseChanges()
    {
        var projectId = await ExecuteDatabaseWritingCommandsAsync(ModelCommandMode.DryRun, failOnSecondCommand: false);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<IfsDbContext>();
        Assert.False(await verifyContext.Projects
            .IgnoreQueryFilters()
            .AnyAsync(project => project.Id == new ProjectId(projectId)));
    }

    [Fact]
    public async Task ModelCommandFailureRollsBackEarlierDatabaseChanges()
    {
        var projectId = await ExecuteDatabaseWritingCommandsAsync(ModelCommandMode.Apply, failOnSecondCommand: true);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<IfsDbContext>();
        Assert.False(await verifyContext.Projects
            .IgnoreQueryFilters()
            .AnyAsync(project => project.Id == new ProjectId(projectId)));
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

    private async Task<int> GetProjectVersionAsync(Guid projectId, Guid organizationId, string token)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v1/projects/{projectId:D}", token, organizationId);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32();
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

    private async Task<Guid> ExecuteDatabaseWritingCommandsAsync(ModelCommandMode mode, bool failOnSecondCommand)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);
        var sender = Substitute.For<ISender>();
        var projectId = Guid.NewGuid();
        var attempts = 0;
        sender.Send(Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<ErrorOr<ModelCommandOutcome>>(HandleCommandAsync()));

        var firstCommand = CreateProjectCommand(projectId);
        var commands = failOnSecondCommand
            ? new IModelCommand[] { firstCommand, CreateProjectCommand(projectId) }
            : [firstCommand];
        var executor = new ModelCommandExecutor(dbContext, sender);
        var result = await executor.ExecuteAsync(commands, mode);

        Assert.Equal(failOnSecondCommand, result.IsError);
        if (failOnSecondCommand)
        {
            Assert.Contains(result.Errors, error => error.Code == "SECOND_COMMAND_FAILED");
        }

        return projectId;

        async Task<ErrorOr<ModelCommandOutcome>> HandleCommandAsync()
        {
            attempts++;
            if (failOnSecondCommand && attempts == 2)
            {
                return Error.Validation("SECOND_COMMAND_FAILED", "The second command failed.");
            }

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var project = ProjectAggregate.Create(
                new ProjectId(attempts == 1 ? projectId : Guid.NewGuid()),
                new OrganizationId(Guid.Parse("33333333-3333-4333-8333-333333333333")),
                $"Probe {suffix}",
                $"p{suffix}",
                new UserProfileId(Guid.NewGuid()));
            Assert.False(project.IsError);
            dbContext.Projects.Add(project.Value);
            await dbContext.SaveChangesAsync();
            return new ModelCommandOutcome($"Project {project.Value.Id.Value:D} written.", []);
        }
    }

    private static CreateProjectCommand CreateProjectCommand(Guid projectId) =>
        new(projectId, Guid.NewGuid(), "Probe project", "probe", string.Empty, "2026.10-pilot", null, [], []);

    private async Task<HttpResponseMessage> AddEnvironmentAsync(string token, Guid organizationId, object body)
    {
        var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/environments",
            token,
            organizationId,
            body);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request);
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
