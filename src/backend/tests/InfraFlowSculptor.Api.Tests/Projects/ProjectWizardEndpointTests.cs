using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Projects;

public sealed class ProjectWizardEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private const string AliceObjectId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string ChloeObjectId = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task WizardDraftCanBeResumedByTheSameUserButIsIsolatedFromAnotherMember()
    {
        await SeedAsync();
        var aliceToken = CreateToken(AliceObjectId, "Alice Martin");
        var chloeToken = CreateToken(ChloeObjectId, "Chloé Petit");
        var content = new { step = 5, values = new { name = "Boutique en ligne", code = "shop" } };

        using var saveRequest = CreateOrganizationRequest(
            HttpMethod.Put,
            "/v1/projects/wizard-draft",
            aliceToken,
            content: new { content });
        using var saveResponse = await client.SendAsync(saveRequest);
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var savedDraft = await saveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, savedDraft.GetProperty("content").GetProperty("step").GetInt32());
        Assert.True(savedDraft.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        using var aliceReadRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            "/v1/projects/wizard-draft",
            aliceToken);
        using var aliceReadResponse = await client.SendAsync(aliceReadRequest);
        Assert.Equal(HttpStatusCode.OK, aliceReadResponse.StatusCode);
        var resumedDraft = await aliceReadResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("shop", resumedDraft.GetProperty("content").GetProperty("values").GetProperty("code").GetString());

        using var chloeReadRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            "/v1/projects/wizard-draft",
            chloeToken);
        using var chloeReadResponse = await client.SendAsync(chloeReadRequest);
        Assert.Equal(HttpStatusCode.NoContent, chloeReadResponse.StatusCode);

        using var discardRequest = CreateOrganizationRequest(
            HttpMethod.Delete,
            "/v1/projects/wizard-draft",
            aliceToken);
        using var discardResponse = await client.SendAsync(discardRequest);
        Assert.Equal(HttpStatusCode.NoContent, discardResponse.StatusCode);

        using var emptyReadRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            "/v1/projects/wizard-draft",
            aliceToken);
        using var emptyReadResponse = await client.SendAsync(emptyReadRequest);
        Assert.Equal(HttpStatusCode.NoContent, emptyReadResponse.StatusCode);
    }

    [Fact]
    public async Task WizardCreationIsAtomicAndCreatesProjectEnvironmentsAndOwner()
    {
        await SeedAsync();
        var token = CreateToken(AliceObjectId, "Alice Martin");
        var projectCode = $"wiz{Guid.NewGuid():N}"[..9];
        var requestBody = new
        {
            name = "Wizard Atomicity",
            code = projectCode,
            tenantId = Guid.Parse(TenantId),
            description = "Created by the project wizard",
            namingPreset = "Compact",
            defaultNameTemplate = "{abbr}-{project}-{name}-{env}",
            resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
            environments = new[]
            {
                new { name = "Development", code = "dev", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), description = "" },
                new { name = "Development duplicate", code = "dev", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), description = "" }
            },
            tags = new Dictionary<string, string>(),
            publishingPreset = (string?)null
        };

        using var saveDraftRequest = CreateOrganizationRequest(
            HttpMethod.Put,
            "/v1/projects/wizard-draft",
            token,
            new { content = new { step = 6, name = "Wizard Atomicity" } });
        using var saveDraftResponse = await client.SendAsync(saveDraftRequest);
        Assert.Equal(HttpStatusCode.OK, saveDraftResponse.StatusCode);

        using var invalidCreateRequest = CreateOrganizationRequest(
            HttpMethod.Post,
            "/v1/projects/from-wizard",
            token,
            requestBody);
        invalidCreateRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var invalidCreateResponse = await client.SendAsync(invalidCreateRequest);
        Assert.Equal(HttpStatusCode.Conflict, invalidCreateResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            Assert.False(await dbContext.Projects.IgnoreQueryFilters().AnyAsync(project => project.Code == projectCode));
        }

        using var retainedDraftRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            "/v1/projects/wizard-draft",
            token);
        using var retainedDraftResponse = await client.SendAsync(retainedDraftRequest);
        Assert.Equal(HttpStatusCode.OK, retainedDraftResponse.StatusCode);

        requestBody = new
        {
            name = "Wizard Atomicity",
            code = projectCode,
            tenantId = Guid.Parse(TenantId),
            description = "Created by the project wizard",
            namingPreset = "Compact",
            defaultNameTemplate = "{abbr}-{project}-{name}-{env}",
            resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
            environments = new[]
            {
                new { name = "Development", code = "dev", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), description = "" },
                new { name = "Production", code = "prd", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = true, approvers = new[] { "Shop Release Approvers" }, description = "" }
            },
            tags = new Dictionary<string, string> { ["costCenter"] = "commerce" },
            publishingPreset = (string?)null
        };
        var idempotencyKey = Guid.NewGuid().ToString("N");
        using var createRequest = CreateOrganizationRequest(
            HttpMethod.Post,
            "/v1/projects/from-wizard",
            token,
            requestBody);
        createRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        using var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = created.GetProperty("id").GetGuid();

        using (var replayRequest = CreateOrganizationRequest(
                   HttpMethod.Post,
                   "/v1/projects/from-wizard",
                   token,
                   requestBody))
        {
            replayRequest.Headers.Add("Idempotency-Key", idempotencyKey);
            using var replayResponse = await client.SendAsync(replayRequest);
            Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
            var replayed = await replayResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(projectId, replayed.GetProperty("id").GetGuid());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var currentOrganization = scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>();
            currentOrganization.Set(new OrganizationId(OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var project = await dbContext.Projects
                .Include(candidate => candidate.Environments)
                .Include(candidate => candidate.RoleAssignments)
                .SingleAsync(candidate => candidate.Id == new ProjectId(projectId));
            var aliceProfile = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(AliceObjectId));

            Assert.Equal("Compact", project.NamingPreset);
            Assert.Equal("{abbr}-{project}-{name}-{env}", project.DefaultNameTemplate);
            Assert.Equal("commerce", project.Tags.Single().Value);
            Assert.Equal(2, project.Environments.Count);
            Assert.True(project.Environments.Single(environment => environment.Code == "prd").IsProtected);
            Assert.Contains(project.RoleAssignments, assignment =>
                assignment.Principal == aliceProfile.Id && assignment.Role == ProjectRole.Owner);
            Assert.Empty(await dbContext.ProjectWizardDrafts.ToArrayAsync());
        }

        using var deletedDraftRequest = CreateOrganizationRequest(
            HttpMethod.Get,
            "/v1/projects/wizard-draft",
            token);
        using var deletedDraftResponse = await client.SendAsync(deletedDraftRequest);
        Assert.Equal(HttpStatusCode.NoContent, deletedDraftResponse.StatusCode);
    }

    [Fact]
    public async Task NamingPreviewUsesTheEngineAndReturnsValidationIssues()
    {
        await SeedAsync();
        var token = CreateToken(AliceObjectId, "Alice Martin");
        var requestBody = new
        {
            projectName = "Boutique en ligne",
            projectCode = "shop",
            namingPreset = "Compact",
            defaultNameTemplate = "{abbr}-{project}-{name}-{env}",
            resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
            environments = new[] { new { code = "dev", region = "francecentral" } },
            tags = new Dictionary<string, string>()
        };

        using var request = CreateOrganizationRequest(HttpMethod.Post, "/v1/naming/preview", token, requestBody);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("kv-shop-main-dev", preview.GetProperty("items")[0].GetProperty("resourceName").GetString());

        requestBody = new
        {
            projectName = "Boutique en ligne",
            projectCode = "shop",
            namingPreset = "Custom",
            defaultNameTemplate = "{abbr}-{project}-{name}-{zone}",
            resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
            environments = new[] { new { code = "dev", region = "francecentral" } },
            tags = new Dictionary<string, string>()
        };
        using var invalidRequest = CreateOrganizationRequest(HttpMethod.Post, "/v1/naming/preview", token, requestBody);
        using var invalidResponse = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidPreview = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            "NAMING_TOKEN_UNKNOWN",
            invalidPreview.GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("code").GetString()));
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);
    }

    private static string CreateToken(string objectId, string displayName) => TestTokens.CreateAccessToken(
        tenantId: TenantId,
        objectId: objectId,
        displayName: displayName,
        email: displayName == "Alice Martin" ? "alice@contoso.example" : "chloe@contoso.example");

    private static HttpRequestMessage CreateOrganizationRequest(
        HttpMethod method,
        string path,
        string token,
        object? content = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = content is null ? null : JsonContent.Create(content)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Ifs-Organization", OrganizationId.ToString("D"));
        return request;
    }
}
