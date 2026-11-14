using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Projects;

public sealed class RevisionsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private const string AliceObjectId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GenerateRevisionReturnsTheLastRevisionWhenTheModelHasNotChanged()
    {
        await SeedAsync(includeProjects: true, seedReferenceModel: true);
        var path = $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/revisions";

        using var firstRequest = CreateRequest(HttpMethod.Post, path, Guid.NewGuid().ToString("N"));
        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, first.GetProperty("number").GetInt32());
        Assert.False(first.GetProperty("isUnchanged").GetBoolean());

        using (var auditScope = factory.Services.CreateScope())
        {
            auditScope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(OrganizationId));
            var audit = await auditScope.ServiceProvider.GetRequiredService<IfsDbContext>().AuditEvents
                .SingleAsync(entry => entry.Action == AuditActions.RevisionGenerationRequested
                    && entry.ProjectId == DemoDataSeeder.ShopProjectId.Value);
            Assert.Equal("Revision", audit.ObjectType.ToString());
        }

        using var listRequest = CreateRequest(HttpMethod.Get, path);
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetProperty("nextNumber").GetInt32());
        Assert.Single(list.GetProperty("items").EnumerateArray());

        using var unchangedRequest = CreateRequest(HttpMethod.Post, path, Guid.NewGuid().ToString("N"));
        using var unchangedResponse = await client.SendAsync(unchangedRequest);
        Assert.Equal(HttpStatusCode.OK, unchangedResponse.StatusCode);
        var unchanged = await unchangedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("revisionId").GetGuid(), unchanged.GetProperty("revisionId").GetGuid());
        Assert.Equal(1, unchanged.GetProperty("number").GetInt32());
        Assert.True(unchanged.GetProperty("isUnchanged").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var revision = await dbContext.Set<RevisionAggregate>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.ProjectId == DemoDataSeeder.ShopProjectId);
        Assert.Equal(1, await dbContext.Set<RevisionAggregate>().IgnoreQueryFilters()
            .CountAsync(candidate => candidate.ProjectId == DemoDataSeeder.ShopProjectId));
        var frozenInput = await dbContext.Set<RevisionInputEntity>().IgnoreQueryFilters()
            .SingleAsync(input => input.RevisionId == revision.Id);

        var currentProject = await dbContext.Projects.IgnoreQueryFilters()
            .SingleAsync(project => project.Id == DemoDataSeeder.ShopProjectId);
        var changed = currentProject.UpdateSettings(
            "Boutique en ligne v11",
            "shop11",
            currentProject.Description,
            currentProject.SystemTagsEnabled,
            currentProject.CoordinatorUserProfileId,
            currentProject.Tags.Select(tag => new KeyValuePair<string, string>(tag.Key, tag.Value)),
            confirmImpact: true);
        Assert.False(changed.IsError);
        currentProject.RecordModelChange();
        await dbContext.SaveChangesAsync();

        using var changedModelRequest = CreateRequest(HttpMethod.Post, path, Guid.NewGuid().ToString("N"));
        using var changedModelResponse = await client.SendAsync(changedModelRequest);
        Assert.Equal(HttpStatusCode.Accepted, changedModelResponse.StatusCode);
        var changedModel = await changedModelResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, changedModel.GetProperty("number").GetInt32());
        Assert.False(changedModel.GetProperty("isUnchanged").GetBoolean());

        var frozenSnapshot = RevisionInputJsonSerializer.Deserialize(frozenInput.Snapshot);
        Assert.Equal(revision.ModelVersion, frozenInput.ModelVersion);
        Assert.Equal(revision.ModelVersion, frozenSnapshot.Project.ModelVersion);
        Assert.Equal("shop", frozenSnapshot.Project.Code);
        Assert.Equal("shop11", currentProject.Code);
        Assert.True(currentProject.ModelVersion > frozenInput.ModelVersion);
    }

    [Fact]
    public async Task ListedQueuePositionIncludesEarlierRevisionsFromOtherProjectsInTheOrganization()
    {
        await SeedAsync(includeProjects: true, seedReferenceModel: false);
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var shop = await dbContext.Projects.IgnoreQueryFilters()
                .SingleAsync(project => project.Id == DemoDataSeeder.ShopProjectId);
            var owner = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(AliceObjectId));
            var otherProject = ProjectAggregate.Create(
                new ProjectId(Guid.NewGuid()),
                new OrganizationId(OrganizationId),
                "Queue Position Project",
                "queuepos",
                Guid.Parse(TenantId),
                string.Empty,
                shop.CatalogVersion,
                owner.Id).Value;
            dbContext.Projects.Add(otherProject);

            var author = new UserKey(Guid.Parse(TenantId), Guid.Parse(AliceObjectId));
            var earlierRevision = RevisionAggregate.Queue(
                RevisionId.New(TimeProvider.System),
                new OrganizationId(OrganizationId),
                otherProject.Id,
                1,
                author,
                now,
                otherProject.ModelVersion,
                otherProject.CatalogVersion,
                otherProject.InfrastructureLanguage,
                otherProject.CiPlatform,
                new string('a', 64));
            var listedRevision = RevisionAggregate.Queue(
                RevisionId.New(TimeProvider.System),
                new OrganizationId(OrganizationId),
                shop.Id,
                1,
                author,
                now.AddSeconds(1),
                shop.ModelVersion,
                shop.CatalogVersion,
                shop.InfrastructureLanguage,
                shop.CiPlatform,
                new string('b', 64));
            dbContext.Set<RevisionAggregate>().AddRange(earlierRevision, listedRevision);
            await dbContext.SaveChangesAsync();
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/revisions");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var listed = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(2, listed.GetProperty("queuePosition").GetInt32());
    }

    [Fact]
    public async Task ReadingRevisionFileDoesNotRequireDownloadQueryParameter()
    {
        await SeedAsync(includeProjects: true, seedReferenceModel: true);
        var now = TimeProvider.System.GetUtcNow();
        RevisionId revisionId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var project = await dbContext.Projects.IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == DemoDataSeeder.ShopProjectId);
            var revisionCount = await dbContext.Set<RevisionAggregate>().IgnoreQueryFilters()
                .CountAsync(candidate => candidate.ProjectId == project.Id);
            revisionId = RevisionId.New(TimeProvider.System);
            var revision = RevisionAggregate.Queue(
                revisionId,
                new OrganizationId(OrganizationId),
                DemoDataSeeder.ShopProjectId,
                revisionCount + 1,
                new UserKey(Guid.Parse(TenantId), Guid.Parse(AliceObjectId)),
                now,
                project.ModelVersion,
                project.CatalogVersion,
                project.InfrastructureLanguage,
                project.CiPlatform,
                new string('a', 64));
            revision.MarkGenerating(now);
            revision.MarkGenerated("{}", [], [], [], now);
            dbContext.Set<RevisionAggregate>().Add(revision);
            await dbContext.SaveChangesAsync();
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/revisions/{revisionId.Value:D}/files/orders/infra/missing.bicep");
        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateRevisionRejectsBlockingFindingsWithoutQueueingWork()
    {
        await SeedAsync(includeProjects: false, seedReferenceModel: false);
        var projectId = new ProjectId(Guid.NewGuid());
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var owner = await dbContext.UserProfiles.SingleAsync(profile => profile.ObjectId == Guid.Parse(AliceObjectId));
            var project = ProjectAggregate.Create(
                projectId,
                new OrganizationId(OrganizationId),
                "Project Without Environments",
                "emptyproj",
                Guid.Parse(TenantId),
                string.Empty,
                "2026.10-pilot.6",
                owner.Id).Value;
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();
        }

        var path = $"/v1/projects/{projectId.Value:D}/revisions";
        using var request = CreateRequest(HttpMethod.Post, path, Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GENERATION_BLOCKED", problem.GetProperty("code").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var revisions = await verificationContext.Set<RevisionAggregate>().IgnoreQueryFilters()
            .Where(revision => revision.ProjectId == projectId)
            .ToArrayAsync();
        Assert.Empty(revisions);
        var revisionIds = revisions.Select(revision => revision.Id).ToArray();
        Assert.Empty(await verificationContext.Set<RevisionInputEntity>().IgnoreQueryFilters()
            .Where(input => revisionIds.Contains(input.RevisionId))
            .ToArrayAsync());
    }

    private async Task SeedAsync(bool includeProjects, bool seedReferenceModel)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var revisions = await dbContext.Set<RevisionAggregate>().IgnoreQueryFilters()
            .Where(revision => revision.ProjectId == DemoDataSeeder.ShopProjectId)
            .ToArrayAsync();
        var revisionIds = revisions.Select(revision => revision.Id).ToArray();
        var inputs = await dbContext.Set<RevisionInputEntity>().IgnoreQueryFilters()
            .Where(input => revisionIds.Contains(input.RevisionId))
            .ToArrayAsync();
        dbContext.RemoveRange(inputs);
        dbContext.RemoveRange(revisions);
        await dbContext.SaveChangesAsync();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System, includeProjects, seedReferenceModel);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.CreateAccessToken(
                tenantId: TenantId,
                objectId: AliceObjectId,
                displayName: "Alice Martin",
                email: "alice@contoso.example"));
        request.Headers.Add("X-Ifs-Organization", OrganizationId.ToString("D"));
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }
}
