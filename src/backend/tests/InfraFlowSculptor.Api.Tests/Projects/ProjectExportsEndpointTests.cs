using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Json.Schema;

namespace InfraFlowSculptor.Api.Tests.Projects;

public sealed class ProjectExportsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private const string AliceObjectId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task ExportProjectReturnsDownloadableModelSnapshot()
    {
        await SeedAsync();
        var projectId = DemoDataSeeder.ShopProjectId;
        string expected;
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(OrganizationId));
            var snapshotResult = await scope.ServiceProvider.GetRequiredService<ModelSnapshotBuilder>()
                .BuildAsync(projectId.Value);
            Assert.False(snapshotResult.IsError, string.Join("; ", snapshotResult.Errors.Select(error => error.Code)));
            expected = RevisionInputJsonSerializer.Serialize(snapshotResult.Value);
        }

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{projectId.Value:D}/export",
            Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var downloadUrl = body.GetProperty("downloadUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(downloadUrl));
        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>()
                .Set(new OrganizationId(OrganizationId));
            var audit = await scope.ServiceProvider.GetRequiredService<IfsDbContext>().AuditEvents
                .SingleAsync(entry => entry.Action == AuditActions.ProjectExported
                    && entry.ProjectId == projectId.Value);
            Assert.Equal("Project", audit.ObjectType.ToString());
            using var auditDiff = JsonDocument.Parse(audit.Diff);
            Assert.Equal("shop", auditDiff.RootElement.GetProperty("code").GetProperty("after").GetString());
        }

        using var downloadResponse = await client.GetAsync(downloadUrl);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/json", downloadResponse.Content.Headers.ContentType?.MediaType);
        var exportedJson = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal(expected, exportedJson);
        Assert.DoesNotContain("members", exportedJson, StringComparison.OrdinalIgnoreCase);
        var schemaResourceName = typeof(CatalogLoader).Assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("ifs-project-v1.schema.json", StringComparison.Ordinal));
        Assert.True(schemaResourceName is not null, string.Join(", ", typeof(CatalogLoader).Assembly.GetManifestResourceNames()));
        using var schemaStream = typeof(CatalogLoader).Assembly.GetManifestResourceStream(schemaResourceName!);
        Assert.NotNull(schemaStream);
        using var schemaReader = new StreamReader(schemaStream);
        var schema = JsonSchema.FromText(await schemaReader.ReadToEndAsync());
        using var exportDocument = JsonDocument.Parse(exportedJson);
        var schemaResult = schema.Evaluate(exportDocument.RootElement);
        Assert.True(schemaResult.IsValid, schemaResult.ToString());
        Assert.Equal(expected, RevisionInputJsonSerializer.Serialize(
            RevisionInputJsonSerializer.Deserialize(exportedJson)));
    }

    [Fact]
    public async Task ExpiredProjectExportCannotBeDownloaded()
    {
        var token = await factory.Services.GetRequiredService<IProjectExportStore>().SaveAsync(
            "{}"u8.ToArray(),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            CancellationToken.None);

        using var response = await client.GetAsync($"/v1/project-exports/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExportProjectRequiresProjectAdministratorPermission()
    {
        await SeedAsync();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/export",
            Guid.NewGuid().ToString("N"),
            "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
            "Bob Bernard",
            "bob@contoso.example");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System, includeProjects: true, seedReferenceModel: true);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string idempotencyKey,
        string objectId = AliceObjectId,
        string displayName = "Alice Martin",
        string email = "alice@contoso.example")
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.CreateAccessToken(
                tenantId: TenantId,
                objectId: objectId,
                displayName: displayName,
                email: email));
        request.Headers.Add("X-Ifs-Organization", OrganizationId.ToString("D"));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }
}
