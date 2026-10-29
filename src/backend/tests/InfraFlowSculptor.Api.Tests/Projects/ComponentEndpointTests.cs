using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Contracts.Projects;
using InfraFlowSculptor.Contracts.Resources;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using ComponentRoot = InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraFlowSculptor.Api.Tests.Projects;

public sealed class ComponentEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly SemaphoreSlim TestLock = new(1, 1);
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = DemoDataSeeder.ShopProjectId.Value;
    private static readonly string AliceToken = TestTokens.CreateAccessToken();
    private static readonly string[] AliceApprovers = ["alice@contoso.example"];
    private static readonly string[] KeyVaultSecretUserRoles = ["Key Vault Secrets User"];
    private static readonly string[] ReaderRoleNames = ["Reader"];
    private static readonly string[] DuplicateKeyVaultSecretUserRoles = [" key vault secrets user "];
    private static readonly string[] WhitespaceKeyVaultSecretUserRoles = [" Key Vault Secrets User "];
    private static readonly string[] DuplicateRoleEntries = ["Key Vault Secrets User", " key vault secrets user "];
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public Task ComponentAndResourceGroupCanBeCreatedAndListed() =>
        RunSerialAsync(ComponentAndResourceGroupCanBeCreatedAndListedCore);

    [Fact]
    public Task ResourceValuesRoundTripAndIrreversibleOverrideCannotBeReversed() =>
        RunSerialAsync(ResourceValuesRoundTripAndIrreversibleOverrideCannotBeReversedCore);

    [Fact]
    public Task ResourceChildCanBeAddedUpdatedAndRemoved() =>
        RunSerialAsync(ResourceChildCanBeAddedUpdatedAndRemovedCore);

    [Fact]
    public Task SingleTargetResourceUsesThePersistedSingleTargetId() =>
        RunSerialAsync(SingleTargetResourceUsesThePersistedSingleTargetIdCore);

    [Fact]
    public Task EffectiveValuesExposeTargetPresenceTagsAndPropertyProvenance() =>
        RunSerialAsync(EffectiveValuesExposeTargetPresenceTagsAndPropertyProvenanceCore);

    [Fact]
    public Task ResourceDeletionImpactPreviewIsServerCalculatedAndReportsPartialCoverage() =>
        RunSerialAsync(ResourceDeletionImpactPreviewIsServerCalculatedAndReportsPartialCoverageCore);

    [Fact]
    public Task ResourceLinksCanBeCreatedListedAndPreviewedBeforeRemoval() =>
        RunSerialAsync(ResourceLinksCanBeCreatedListedAndPreviewedBeforeRemovalCore);

    [Fact]
    public Task ResourceLinkUpdateTrimsRolesBeforeCalculatingImpact() =>
        RunSerialAsync(ResourceLinkUpdateTrimsRolesBeforeCalculatingImpactCore);

    [Fact]
    public Task ResourceLinkCanBeRemovedAfterReviewingImpact() =>
        RunSerialAsync(ResourceLinkCanBeRemovedAfterReviewingImpactCore);

    [Fact]
    public Task ConcurrentHostingLinksRespectComponentVersionAndCardinality() =>
        RunSerialAsync(ConcurrentHostingLinksRespectComponentVersionAndCardinalityCore);

    [Fact]
    public Task StaleComponentVersionWinsOverLinkCardinalityValidation() =>
        RunSerialAsync(StaleComponentVersionWinsOverLinkCardinalityValidationCore);

    [Fact]
    public Task DeletingDefaultLogAnalyticsWorkspaceClearsComponentReference() =>
        RunSerialAsync(DeletingDefaultLogAnalyticsWorkspaceClearsComponentReferenceCore);

    [Fact]
    public Task DefaultLogAnalyticsRejectsIncompatibleTargetComponents() =>
        RunSerialAsync(DefaultLogAnalyticsRejectsIncompatibleTargetComponentsCore);

    [Fact]
    public Task DeletingUserAssignedIdentityUsedByALinkIsBlocked() =>
        RunSerialAsync(DeletingUserAssignedIdentityUsedByALinkIsBlockedCore);

    [Fact]
    public Task DeletingComponentWithIncomingLinksIsBlocked() =>
        RunSerialAsync(DeletingComponentWithIncomingLinksIsBlockedCore);

    [Fact]
    public Task NewResourceNamePreviewUsesTheServerNamingEngine() =>
        RunSerialAsync(NewResourceNamePreviewUsesTheServerNamingEngineCore);

    [Fact]
    public Task ResourceMetadataCanBeUpdatedWithOptimisticConcurrency() =>
        RunSerialAsync(ResourceMetadataCanBeUpdatedWithOptimisticConcurrencyCore);

    [Fact]
    public Task ResourceMetadataRejectsNullTagsAndValues() =>
        RunSerialAsync(ResourceMetadataRejectsNullTagsAndValuesCore);

    [Fact]
    public Task SuspiciousApplicationSettingLiteralIsRejected() =>
        RunSerialAsync(SuspiciousApplicationSettingLiteralIsRejectedCore);

    [Fact]
    public Task ForcedSuspiciousApplicationSettingIsAuditedWithoutItsValue() =>
        RunSerialAsync(ForcedSuspiciousApplicationSettingIsAuditedWithoutItsValueCore);

    [Fact]
    public Task PilotPipelineSecretUsesExpectedVariableAndImplicitSecretRead() =>
        RunSerialAsync(PilotPipelineSecretUsesExpectedVariableAndImplicitSecretReadCore);

    [Fact]
    public Task ApplicationSettingLiteralCanBeUpdatedAndDeleted() =>
        RunSerialAsync(ApplicationSettingLiteralCanBeUpdatedAndDeletedCore);

    [Fact]
    public Task PipelineSecretVaultCannotShareConsumerComponent() =>
        RunSerialAsync(PipelineSecretVaultCannotShareConsumerComponentCore);

    [Fact]
    public Task ProjectNamingSettingsCanBeSavedAndPreviewUsesComponentOverrides() =>
        RunSerialAsync(ProjectNamingSettingsCanBeSavedAndPreviewUsesComponentOverridesCore);

    [Fact]
    public Task NamingPreviewRejectsTypesOutsideThePinnedCatalog() =>
        RunSerialAsync(NamingPreviewRejectsTypesOutsideThePinnedCatalogCore);

    [Fact]
    public Task NamingPreviewReportsGlobalNamesReservedBySiblingProjects() =>
        RunSerialAsync(NamingPreviewReportsGlobalNamesReservedBySiblingProjectsCore);

    private async Task NewResourceNamePreviewUsesTheServerNamingEngineCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);

        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            using var request = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/preview-name",
                new { resourceGroupId, type = "KeyVault", logicalName = "preview-vault" });
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var item = Assert.Single(content.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(componentId, item.GetProperty("componentId").GetGuid());
            Assert.Equal("KeyVault", item.GetProperty("resourceType").GetString());
            Assert.Equal("preview-vault", item.GetProperty("logicalName").GetString());
            Assert.Equal(environment.Code, item.GetProperty("targetCode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("resourceName").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("resourceGroupName").GetString()));

            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                "preview-vault",
                new Dictionary<string, object?>());
            using var resource = JsonDocument.Parse(resourceBody);
            var resourceId = resource.RootElement.GetProperty("id").GetGuid();
            using var existingResourceRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/preview-name",
                new { resourceGroupId, type = "KeyVault", logicalName = "preview-vault", resourceId });
            using var existingResourceResponse = await client.SendAsync(existingResourceRequest);

            Assert.Equal(HttpStatusCode.OK, existingResourceResponse.StatusCode);
            using var existingPreview = await JsonDocument.ParseAsync(await existingResourceResponse.Content.ReadAsStreamAsync());
            var existingItem = Assert.Single(existingPreview.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(resourceId, existingItem.GetProperty("resourceId").GetGuid());
            Assert.Equal(item.GetProperty("resourceName").GetString(), existingItem.GetProperty("resourceName").GetString());

            const string longSiblingName = "verylonglogicalnameresourcename";
            var siblingBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                longSiblingName,
                new Dictionary<string, object?>());
            using var sibling = JsonDocument.Parse(siblingBody);
            var siblingId = sibling.RootElement.GetProperty("id").GetGuid();

            using var siblingPreviewRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/preview-name",
                new { resourceGroupId, type = "KeyVault", logicalName = longSiblingName, resourceId = siblingId });
            using var siblingPreviewResponse = await client.SendAsync(siblingPreviewRequest);
            Assert.Equal(HttpStatusCode.OK, siblingPreviewResponse.StatusCode);
            using var siblingPreview = await JsonDocument.ParseAsync(await siblingPreviewResponse.Content.ReadAsStreamAsync());
            Assert.Contains(
                siblingPreview.RootElement.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("resourceId").GetGuid() == siblingId);

            using var candidatePreviewRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/preview-name",
                new { resourceGroupId, type = "KeyVault", logicalName = "short-candidate" });
            using var candidatePreviewResponse = await client.SendAsync(candidatePreviewRequest);
            Assert.Equal(HttpStatusCode.OK, candidatePreviewResponse.StatusCode);
            using var candidatePreview = await JsonDocument.ParseAsync(await candidatePreviewResponse.Content.ReadAsStreamAsync());
            Assert.DoesNotContain(
                candidatePreview.RootElement.GetProperty("issues").EnumerateArray(),
                issue => issue.TryGetProperty("resourceId", out var issueResourceId)
                    && issueResourceId.ValueKind == JsonValueKind.String
                    && issueResourceId.GetGuid() == siblingId);
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ResourceMetadataCanBeUpdatedWithOptimisticConcurrencyCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                "metadata-vault",
                new Dictionary<string, object?>());
            using var created = JsonDocument.Parse(resourceBody);
            var resourceId = created.RootElement.GetProperty("id").GetGuid();
            var version = created.RootElement.GetProperty("version").GetInt32();
            var tags = new Dictionary<string, string> { ["team"] = "platform" };

            using var request = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/metadata",
                new { description = "Vault de la recette", tags, expectedVersion = version });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var updated = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal("Vault de la recette", updated.RootElement.GetProperty("description").GetString());
            Assert.Equal("platform", updated.RootElement.GetProperty("tags").GetProperty("team").GetString());
            Assert.True(updated.RootElement.GetProperty("version").GetInt32() > version);

            using var staleRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/metadata",
                new { description = "Modification périmée", tags, expectedVersion = version });
            staleRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var staleResponse = await client.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

            using var resourceRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}");
            using var resourceResponse = await client.SendAsync(resourceRequest);
            Assert.Equal(HttpStatusCode.OK, resourceResponse.StatusCode);
            using var current = await JsonDocument.ParseAsync(await resourceResponse.Content.ReadAsStreamAsync());
            var currentResource = Assert.Single(
                current.RootElement.GetProperty("resources").EnumerateArray(),
                item => item.GetProperty("id").GetGuid() == resourceId);
            Assert.Equal("Vault de la recette", currentResource.GetProperty("description").GetString());
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ResourceMetadataRejectsNullTagsAndValuesCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                "invalid-metadata-vault",
                new Dictionary<string, object?>());
            using var created = JsonDocument.Parse(resourceBody);
            var resourceId = created.RootElement.GetProperty("id").GetGuid();
            var version = created.RootElement.GetProperty("version").GetInt32();
            var initialDescription = created.RootElement.GetProperty("description").GetString();
            var initialTags = created.RootElement.GetProperty("tags").EnumerateObject()
                .ToDictionary(tag => tag.Name, tag => tag.Value.GetString(), StringComparer.Ordinal);
            object[] invalidRequests =
            [
                new { description = "Invalid null tags", tags = (object?)null, expectedVersion = version },
                new
                {
                    description = "Invalid null tag value",
                    tags = new Dictionary<string, string?> { ["team"] = null },
                    expectedVersion = version
                }
            ];

            foreach (var invalidRequest in invalidRequests)
            {
                using var request = CreateRequest(
                    HttpMethod.Put,
                    $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/metadata",
                    invalidRequest);
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                using var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("RESOURCE_TAG_FORMAT", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }

            using var currentRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}");
            using var currentResponse = await client.SendAsync(currentRequest);
            Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
            using var current = await JsonDocument.ParseAsync(await currentResponse.Content.ReadAsStreamAsync());
            var currentResource = Assert.Single(
                current.RootElement.GetProperty("resources").EnumerateArray(),
                item => item.GetProperty("id").GetGuid() == resourceId);
            Assert.Equal(initialDescription, currentResource.GetProperty("description").GetString());
            Assert.Equal(version, currentResource.GetProperty("version").GetInt32());
            var currentTags = currentResource.GetProperty("tags").EnumerateObject()
                .ToDictionary(tag => tag.Name, tag => tag.Value.GetString(), StringComparer.Ordinal);
            Assert.Equal(initialTags.OrderBy(tag => tag.Key), currentTags.OrderBy(tag => tag.Key));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task SuspiciousApplicationSettingLiteralIsRejectedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var applicationBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>());
            using var application = JsonDocument.Parse(applicationBody);
            var applicationResourceId = application.RootElement.GetProperty("id").GetGuid();
            const string token = "ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            using var request = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationResourceId:D}/app-settings",
                new
                {
                    name = "Test__Token",
                    sourceKind = "Literal",
                    literalValue = token,
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false
                });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(body.Contains("VAL-PAR-SECRET-SUSPECT", StringComparison.Ordinal), body);

            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            Assert.False(await dbContext.AppSettings.AnyAsync(setting => setting.ApplicationResourceId == applicationResourceId));
            Assert.False(await dbContext.AuditEvents.AnyAsync(audit => audit.Diff.Contains(token)));
            Assert.DoesNotContain(await dbContext.OutboxMessages.ToArrayAsync(), message => message.Payload.GetRawText().Contains(token, StringComparison.Ordinal));
            Assert.False(await dbContext.IdempotencyKeys.AnyAsync(key => key.Response != null && key.Response.Contains(token)));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ForcedSuspiciousApplicationSettingIsAuditedWithoutItsValueCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var applicationBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>());
            using var application = JsonDocument.Parse(applicationBody);
            var applicationResourceId = application.RootElement.GetProperty("id").GetGuid();
            const string token = "ghp_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            using var request = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationResourceId:D}/app-settings",
                new
                {
                    name = "Test__Token",
                    sourceKind = "Literal",
                    literalValue = token,
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = true
                });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(token, responseBody, StringComparison.Ordinal);
            using var responseDocument = JsonDocument.Parse(responseBody);
            Assert.Contains(responseDocument.RootElement.GetProperty("warnings").EnumerateArray(),
                warning => warning.GetString()!.Contains("VAL-PAR-SECRET-SUSPECT", StringComparison.Ordinal));

            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var setting = await dbContext.AppSettings.SingleAsync(candidate => candidate.ApplicationResourceId == applicationResourceId);
            Assert.Equal(token, setting.LiteralSource!.Value);
            var audits = await dbContext.AuditEvents.Where(candidate => candidate.ObjectId == setting.Id).ToArrayAsync();
            Assert.Contains(audits, audit => audit.Action == "component.app_setting.created");
            Assert.Single(audits, audit => audit.Action == "component.app_setting.value_forced_non_secret");
            Assert.All(audits, audit => Assert.DoesNotContain(token, audit.Diff, StringComparison.Ordinal));
            Assert.False(await dbContext.IdempotencyKeys.AnyAsync(key => key.Response != null && key.Response.Contains(token)));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task PilotPipelineSecretUsesExpectedVariableAndImplicitSecretReadCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        Guid? vaultComponentId = null;
        Guid? applicationComponentId = null;
        try
        {
            vaultComponentId = await CreateResourceTestComponentAsync(environment);
            var vaultGroupId = await CreateResourceTestGroupAsync(vaultComponentId.Value);
            var vaultBody = await CreateResourceAsync(
                vaultComponentId.Value,
                vaultGroupId,
                "KeyVault",
                "main",
                new Dictionary<string, object?>());
            using var vault = JsonDocument.Parse(vaultBody);
            var vaultId = vault.RootElement.GetProperty("id").GetGuid();

            applicationComponentId = await CreateResourceTestComponentAsync(environment);
            var applicationGroupId = await CreateResourceTestGroupAsync(applicationComponentId.Value);
            var identityBody = await CreateResourceAsync(
                applicationComponentId.Value,
                applicationGroupId,
                "UserAssignedIdentity",
                "id-api",
                new Dictionary<string, object?>());
            using var identity = JsonDocument.Parse(identityBody);
            var identityId = identity.RootElement.GetProperty("id").GetGuid();
            var readerIdentityBody = await CreateResourceAsync(
                applicationComponentId.Value,
                applicationGroupId,
                "UserAssignedIdentity",
                "id-secret-reader",
                new Dictionary<string, object?>());
            using var readerIdentity = JsonDocument.Parse(readerIdentityBody);
            var readerIdentityId = readerIdentity.RootElement.GetProperty("id").GetGuid();
            var applicationBody = await CreateResourceAsync(
                applicationComponentId.Value,
                applicationGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>(),
                defaultIdentityResourceId: identityId);
            using var application = JsonDocument.Parse(applicationBody);
            var applicationId = application.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(identityId, application.RootElement.GetProperty("defaultIdentityResourceId").GetGuid());

            using var paddedImplicitNameRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = " AZURE_CLIENT_ID ",
                    sourceKind = "Literal",
                    literalValue = "safe-value",
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false
                });
            paddedImplicitNameRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var paddedImplicitNameResponse = await client.SendAsync(paddedImplicitNameRequest);
            Assert.Equal(HttpStatusCode.BadRequest, paddedImplicitNameResponse.StatusCode);
            Assert.Equal("VAL-PAR-IMPLICITE-CONFLIT", await GetProblemCodeAsync(paddedImplicitNameResponse));

            using var missingNameRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = (string?)null,
                    sourceKind = "Literal",
                    literalValue = "safe-value",
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false
                });
            missingNameRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var missingNameResponse = await client.SendAsync(missingNameRequest);
            Assert.Equal(HttpStatusCode.BadRequest, missingNameResponse.StatusCode);
            Assert.Equal("VAL-PAR-01", await GetProblemCodeAsync(missingNameResponse));

            using var request = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = "Payments__ApiKey",
                    sourceKind = "KeyVaultSecret",
                    literalValue = (string?)null,
                    overrides = (object?)null,
                    sourceResourceId = (Guid?)null,
                    outputName = (string?)null,
                    keyVaultResourceId = (Guid?)vaultId,
                    secretName = "payments-api-key",
                    secretFeedKind = "PipelineSecret",
                    pipelineVariableName = (string?)null,
                    identityResourceId = (Guid?)readerIdentityId,
                    forceNonSecret = false
                });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var created = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal("MAIN_PAYMENTS_API_KEY", created.RootElement.GetProperty("pipelineVariableName").GetString());
            Assert.Equal(vaultId, created.RootElement.GetProperty("keyVaultResourceId").GetGuid());
            Assert.Equal(readerIdentityId, created.RootElement.GetProperty("identityResourceId").GetGuid());

            using var defaultIdentitySettingRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = "Payments__CustomerToken",
                    sourceKind = "KeyVaultSecret",
                    literalValue = (string?)null,
                    overrides = (object?)null,
                    sourceResourceId = (Guid?)null,
                    outputName = (string?)null,
                    keyVaultResourceId = (Guid?)vaultId,
                    secretName = "customer-token",
                    secretFeedKind = "ManagedOutsideIfs",
                    pipelineVariableName = (string?)null,
                    identityResourceId = (Guid?)null,
                    forceNonSecret = false
                });
            defaultIdentitySettingRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var defaultIdentitySettingResponse = await client.SendAsync(defaultIdentitySettingRequest);
            Assert.Equal(HttpStatusCode.Created, defaultIdentitySettingResponse.StatusCode);

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            var secretReads = links.RootElement.GetProperty("outgoing").EnumerateArray()
                .Where(link => link.GetProperty("kind").GetString() == "SecretRead")
                .ToArray();
            var explicitIdentityRead = Assert.Single(secretReads,
                link => link.GetProperty("identityResourceId").GetGuid() == readerIdentityId);
            var defaultIdentityRead = Assert.Single(secretReads,
                link => link.GetProperty("identityResourceId").GetGuid() == identityId);
            Assert.Equal("Implicit", explicitIdentityRead.GetProperty("origin").GetString());
            Assert.Equal(vaultId, explicitIdentityRead.GetProperty("targetResourceId").GetGuid());
            Assert.Contains("Key Vault Secrets User", explicitIdentityRead.GetProperty("roles").EnumerateArray()
                .Select(role => role.GetString()), StringComparer.Ordinal);
            Assert.Equal(vaultId, defaultIdentityRead.GetProperty("targetResourceId").GetGuid());

            using var settingsRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/app-settings");
            using var settingsResponse = await client.SendAsync(settingsRequest);
            Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
            using var settings = await JsonDocument.ParseAsync(await settingsResponse.Content.ReadAsStreamAsync());
            var clientId = Assert.Single(settings.RootElement.EnumerateArray(),
                setting => setting.GetProperty("name").GetString() == "AZURE_CLIENT_ID");
            Assert.Equal(identityId, clientId.GetProperty("sourceResourceId").GetGuid());
            Assert.Equal("clientId", clientId.GetProperty("outputName").GetString());

            using var identityImpactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{identityId:D}/delete-impact");
            using var identityImpactResponse = await client.SendAsync(identityImpactRequest);
            Assert.Equal(HttpStatusCode.OK, identityImpactResponse.StatusCode);
            using var identityImpact = await JsonDocument.ParseAsync(await identityImpactResponse.Content.ReadAsStreamAsync());
            Assert.Equal("Blocked", identityImpact.RootElement.GetProperty("status").GetString());
            Assert.Contains(identityImpact.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("code").GetString() == "APP_SETTING_RESOURCE_IN_USE"
                && item.GetProperty("name").GetString() == "AZURE_CLIENT_ID");

            using var deleteIdentityRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{identityId:D}",
                new { expectedVersion = identity.RootElement.GetProperty("version").GetInt32() });
            deleteIdentityRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteIdentityResponse = await client.SendAsync(deleteIdentityRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deleteIdentityResponse.StatusCode);
            Assert.Equal("APP_SETTING_RESOURCE_IN_USE", await GetProblemCodeAsync(deleteIdentityResponse));

            using var changeDefaultIdentityRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/default-identity",
                new
                {
                    identityResourceId = (Guid?)readerIdentityId,
                    expectedVersion = application.RootElement.GetProperty("version").GetInt32()
                });
            changeDefaultIdentityRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var changeDefaultIdentityResponse = await client.SendAsync(changeDefaultIdentityRequest);
            Assert.Equal(HttpStatusCode.OK, changeDefaultIdentityResponse.StatusCode);
            using var changedDefaultIdentity = await JsonDocument.ParseAsync(await changeDefaultIdentityResponse.Content.ReadAsStreamAsync());
            Assert.Equal(readerIdentityId, changedDefaultIdentity.RootElement.GetProperty("defaultIdentityResourceId").GetGuid());

            using var clearDefaultIdentityRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{applicationComponentId.Value:D}/resources/{applicationId:D}/default-identity",
                new
                {
                    identityResourceId = (Guid?)null,
                    expectedVersion = changedDefaultIdentity.RootElement.GetProperty("version").GetInt32()
                });
            clearDefaultIdentityRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var clearDefaultIdentityResponse = await client.SendAsync(clearDefaultIdentityRequest);
            Assert.Equal(HttpStatusCode.BadRequest, clearDefaultIdentityResponse.StatusCode);
            Assert.Equal("RG-LIA-08-IDENTITY-REQUIRED", await GetProblemCodeAsync(clearDefaultIdentityResponse));

            using var impactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{vaultComponentId.Value:D}/resources/{vaultId:D}/delete-impact");
            using var impactResponse = await client.SendAsync(impactRequest);
            Assert.Equal(HttpStatusCode.OK, impactResponse.StatusCode);
            using var impact = await JsonDocument.ParseAsync(await impactResponse.Content.ReadAsStreamAsync());
            Assert.Equal("Blocked", impact.RootElement.GetProperty("status").GetString());
            Assert.Contains(impact.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("code").GetString() == "APP_SETTING_RESOURCE_IN_USE"
                && item.GetProperty("name").GetString() == "Payments__ApiKey");

            using var deleteVaultRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{vaultComponentId.Value:D}/resources/{vaultId:D}",
                new { expectedVersion = vault.RootElement.GetProperty("version").GetInt32() });
            deleteVaultRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteVaultResponse = await client.SendAsync(deleteVaultRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deleteVaultResponse.StatusCode);
            Assert.Equal("APP_SETTING_RESOURCE_IN_USE", await GetProblemCodeAsync(deleteVaultResponse));
        }
        finally
        {
            if (applicationComponentId is { } applicationId)
            {
                await CleanupResourceTestComponentAsync(applicationId);
            }

            if (vaultComponentId is { } vaultId)
            {
                await CleanupResourceTestComponentAsync(vaultId);
            }
        }
    }

    private async Task ApplicationSettingLiteralCanBeUpdatedAndDeletedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var applicationBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>());
            using var application = JsonDocument.Parse(applicationBody);
            var applicationId = application.RootElement.GetProperty("id").GetGuid();

            using var createRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = "PORT",
                    sourceKind = "Literal",
                    literalValue = "8080",
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false
                });
            createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var createResponse = await client.SendAsync(createRequest);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            using var created = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
            var settingId = created.RootElement.GetProperty("id").GetGuid();

            using var readRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings");
            using var readResponse = await client.SendAsync(readRequest);
            Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
            using var readSettings = await JsonDocument.ParseAsync(await readResponse.Content.ReadAsStreamAsync());
            var readSetting = Assert.Single(readSettings.RootElement.EnumerateArray());
            Assert.Equal("8080", readSetting.GetProperty("literalValue").GetString());
            Assert.False(readSetting.GetProperty("isImplicit").GetBoolean());

            using var updateRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings/{settingId:D}",
                new
                {
                    name = "PORT",
                    sourceKind = "Literal",
                    literalValue = "8081",
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false,
                    expectedVersion = 1
                });
            updateRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var updateResponse = await client.SendAsync(updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updated = await JsonDocument.ParseAsync(await updateResponse.Content.ReadAsStreamAsync());
            Assert.Equal(2, updated.RootElement.GetProperty("version").GetInt32());
            Assert.False(updated.RootElement.TryGetProperty("literalValue", out _));

            using var deleteRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings/{settingId:D}",
                new { expectedVersion = 2 });
            deleteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteResponse = await client.SendAsync(deleteRequest);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            using var getRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings");
            using var getResponse = await client.SendAsync(getRequest);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Empty(await getResponse.Content.ReadFromJsonAsync<AppSettingResponse[]>() ?? []);
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task PipelineSecretVaultCannotShareConsumerComponentCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var vaultBody = await CreateResourceAsync(
                componentId, resourceGroupId, "KeyVault", "main", new Dictionary<string, object?>());
            using var vault = JsonDocument.Parse(vaultBody);
            var vaultId = vault.RootElement.GetProperty("id").GetGuid();
            var identityBody = await CreateResourceAsync(
                componentId, resourceGroupId, "UserAssignedIdentity", "id-api", new Dictionary<string, object?>());
            using var identity = JsonDocument.Parse(identityBody);
            var identityId = identity.RootElement.GetProperty("id").GetGuid();
            var applicationBody = await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerApp", "api", new Dictionary<string, object?>());
            using var application = JsonDocument.Parse(applicationBody);
            var applicationId = application.RootElement.GetProperty("id").GetGuid();

            using var request = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{applicationId:D}/app-settings",
                new
                {
                    name = "Payments__ApiKey",
                    sourceKind = "KeyVaultSecret",
                    keyVaultResourceId = (Guid?)vaultId,
                    secretName = "payments-api-key",
                    secretFeedKind = "PipelineSecret",
                    identityResourceId = (Guid?)identityId,
                    overrides = new Dictionary<Guid, string?>(),
                    forceNonSecret = false
                });
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("VAL-SEC-ORDRE", await GetProblemCodeAsync(response));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task NamingPreviewReportsGlobalNamesReservedBySiblingProjectsCore()
    {
        await PrepareAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var logicalName = $"shared{suffix}";
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        Guid? siblingProjectId = null;
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                logicalName,
                new Dictionary<string, object?>());
            using var resource = JsonDocument.Parse(resourceBody);
            var resourceId = resource.RootElement.GetProperty("id").GetGuid();

            siblingProjectId = await SeedSiblingProjectWithGlobalKeyVaultAsync(
                suffix,
                logicalName,
                environment.Code,
                environment.Region);

            using var previewRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/naming/preview",
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = new[] { new { type = "KeyVault", template = "{abbr}-{name}-{env}", abbreviation = (string?)null } }
                });
            using var previewResponse = await client.SendAsync(previewRequest);

            Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
            using var preview = await JsonDocument.ParseAsync(await previewResponse.Content.ReadAsStreamAsync());
            var issues = preview.RootElement.GetProperty("issues").EnumerateArray();
            Assert.Contains(issues, issue =>
                issue.GetProperty("code").GetString() == "VAL-NOM-COLLISION"
                && issue.GetProperty("message").GetString()!.Contains("dans un autre projet de l’organisation", StringComparison.Ordinal)
                && issue.GetProperty("resourceId").GetGuid() == resourceId
                && issue.GetProperty("value").GetString() == $"kv-{logicalName}-{environment.Code}");
        }
        finally
        {
            await CleanupNamingPreviewFixturesAsync(componentId, siblingProjectId);
        }
    }

    private async Task<Guid> SeedSiblingProjectWithGlobalKeyVaultAsync(
        string suffix,
        string logicalName,
        string environmentCode,
        string region)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var sourceProject = await dbContext.Projects.SingleAsync(project => project.Id == new ProjectId(ProjectId));
        var alice = await dbContext.UserProfiles.SingleAsync(profile =>
            profile.ObjectId == Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
        var siblingProject = ProjectAggregate.Create(
            new ProjectId(Guid.NewGuid()),
            new OrganizationId(OrganizationId),
            $"Reserved names {suffix}",
            $"r{suffix}",
            sourceProject.TenantId,
            string.Empty,
            sourceProject.CatalogVersion,
            alice.Id).Value;
        Assert.False(siblingProject.ConfigureTypeNamingOverride("KeyVault", "{abbr}-{name}-{env}", null).IsError);
        var siblingEnvironment = siblingProject.AddEnvironment(
            Guid.NewGuid(),
            "Development",
            environmentCode,
            Guid.NewGuid(),
            region,
            1,
            false,
            [],
            [],
            "Sibling project naming test").Value;
        var siblingComponent = ComponentRoot.Create(
            new ComponentId(Guid.NewGuid()),
            siblingProject.Id,
            siblingProject.OrganizationId,
            $"Sibling {suffix}",
            $"reserved-{suffix}",
            "Global naming reservation test",
            ComponentDeploymentMode.PerEnvironment,
            siblingProject.Environments).Value;
        var resourceGroup = siblingComponent.AddResourceGroup(Guid.NewGuid(), "main").Value;
        Assert.False(siblingComponent.AddResource(
            Guid.NewGuid(),
            resourceGroup.Id,
            "KeyVault",
            logicalName,
            "Global name reservation test",
            false,
            [siblingEnvironment.Id],
            new Dictionary<string, JsonElement>()).IsError);

        dbContext.Projects.Add(siblingProject);
        dbContext.Components.Add(siblingComponent);
        await dbContext.SaveChangesAsync();
        return siblingProject.Id.Value;
    }

    private async Task CleanupNamingPreviewFixturesAsync(Guid componentId, Guid? siblingProjectId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var componentsToRemove = new List<ComponentRoot>();
        var component = await dbContext.Components
            .Include(value => value.Resources)
            .Include(value => value.ResourceGroups)
            .SingleOrDefaultAsync(value => value.Id == new ComponentId(componentId));
        if (component is not null)
        {
            componentsToRemove.Add(component);
        }

        if (siblingProjectId is { } siblingId)
        {
            componentsToRemove.AddRange(await dbContext.Components
                .Include(value => value.Resources)
                .Include(value => value.ResourceGroups)
                .Where(component => component.ProjectId == new ProjectId(siblingId))
                .ToArrayAsync());
        }

        foreach (var componentToRemove in componentsToRemove)
        {
            foreach (var resource in componentToRemove.Resources.ToArray())
            {
                Assert.False(componentToRemove.RemoveResource(resource.Id).IsError);
            }
        }

        await dbContext.SaveChangesAsync();
        foreach (var componentToRemove in componentsToRemove)
        {
            foreach (var resourceGroup in componentToRemove.ResourceGroups.ToArray())
            {
                Assert.False(componentToRemove.RemoveResourceGroup(resourceGroup.Id, containsResources: false).IsError);
            }
        }

        await dbContext.SaveChangesAsync();
        dbContext.Components.RemoveRange(componentsToRemove);
        if (siblingProjectId is { } projectId)
        {
            var siblingProject = await dbContext.Projects
                .SingleOrDefaultAsync(project => project.Id == new ProjectId(projectId));
            if (siblingProject is not null)
            {
                dbContext.Projects.Remove(siblingProject);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task CleanupResourceTestComponentAsync(Guid componentId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var component = await dbContext.Components
            .Include(value => value.Resources)
            .Include(value => value.ResourceGroups)
            .SingleOrDefaultAsync(value => value.Id == new ComponentId(componentId));
        if (component is null) return;

        var resourceIds = component.Resources.Select(resource => resource.Id).ToArray();
        var links = await dbContext.Set<LinkAggregate>()
            .Where(link => resourceIds.Contains(link.SourceResourceId) || resourceIds.Contains(link.TargetResourceId))
            .ToArrayAsync();
        dbContext.Set<LinkAggregate>().RemoveRange(links);
        var appSettings = await dbContext.AppSettings
            .Where(setting => resourceIds.Contains(setting.ApplicationResourceId)
                || setting.SourceResourceId != null && resourceIds.Contains(setting.SourceResourceId.Value)
                || setting.KeyVaultResourceId != null && resourceIds.Contains(setting.KeyVaultResourceId.Value)
                || setting.IdentityResourceId != null && resourceIds.Contains(setting.IdentityResourceId.Value))
            .ToArrayAsync();
        dbContext.AppSettings.RemoveRange(appSettings);

        foreach (var resource in component.Resources.ToArray())
        {
            Assert.False(component.RemoveResource(resource.Id).IsError);
        }

        await dbContext.SaveChangesAsync();
        foreach (var group in component.ResourceGroups.ToArray())
        {
            Assert.False(component.RemoveResourceGroup(group.Id, containsResources: false).IsError);
        }

        await dbContext.SaveChangesAsync();
        dbContext.Components.Remove(component);
        await dbContext.SaveChangesAsync();
    }

    private async Task NamingPreviewRejectsTypesOutsideThePinnedCatalogCore()
    {
        await PrepareAsync();
        using var preview = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/naming/preview",
            new
            {
                preset = "Compact",
                defaultTemplate = "{abbr}-{project}-{name}-{env}",
                resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                typeOverrides = new[] { new { type = "UnknownType", template = "{abbr}-{name}", abbreviation = (string?)null } }
            });

        using var response = await client.SendAsync(preview);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task ProjectNamingSettingsCanBeSavedAndPreviewUsesComponentOverridesCore()
    {
        await PrepareAsync();
        using var getSettings = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}/naming");
        using var getResponse = await client.SendAsync(getSettings);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var original = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        var originalSettings = original.RootElement.Clone();
        var initialVersion = original.RootElement.GetProperty("version").GetInt32();

        using var updateProjectNaming = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/naming",
            new
            {
                preset = "Compact",
                defaultTemplate = "{abbr}-{project}-{name}-{env}",
                resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                typeOverrides = new[] { new { type = "KeyVault", template = "{abbr}-{project}-{name}-{env}", abbreviation = "vault" } },
                expectedVersion = initialVersion
            });
        updateProjectNaming.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var projectNamingResponse = await client.SendAsync(updateProjectNaming);
        Assert.True(projectNamingResponse.StatusCode == HttpStatusCode.OK, await projectNamingResponse.Content.ReadAsStringAsync());
        using var updatedNaming = await JsonDocument.ParseAsync(await projectNamingResponse.Content.ReadAsStreamAsync());
        Assert.True(updatedNaming.RootElement.GetProperty("version").GetInt32() > initialVersion);
        Assert.Equal("vault", updatedNaming.RootElement.GetProperty("typeOverrides")[0].GetProperty("abbreviation").GetString());

        try
        {
            using var invalidProjectNaming = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/naming",
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = new[] { new { type = "UnknownType", template = "{abbr}-{name}", abbreviation = (string?)null } },
                    expectedVersion = updatedNaming.RootElement.GetProperty("version").GetInt32()
                });
            invalidProjectNaming.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var invalidProjectNamingResponse = await client.SendAsync(invalidProjectNaming);
            Assert.Equal(HttpStatusCode.BadRequest, invalidProjectNamingResponse.StatusCode);

            var environment = await GetEditableEnvironmentAsync();
            var componentId = await CreateResourceTestComponentAsync(environment);
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                "main",
                new Dictionary<string, object?>());
            using var resource = JsonDocument.Parse(resourceBody);
            var resourceId = resource.RootElement.GetProperty("id").GetGuid();
            var componentVersion = await GetComponentVersionFromApiAsync(componentId);

            using var setComponentNaming = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/naming",
                new
                {
                    defaultTemplate = (string?)null,
                    typeOverrides = new[] { new { type = "KeyVault", template = "vault-{component}-{name}-{env}", abbreviation = (string?)null } },
                    expectedVersion = componentVersion
                });
            setComponentNaming.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var componentNamingResponse = await client.SendAsync(setComponentNaming);
            Assert.True(componentNamingResponse.StatusCode == HttpStatusCode.OK, await componentNamingResponse.Content.ReadAsStringAsync());

            using var preview = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/naming/preview",
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = new[] { new { type = "KeyVault", template = "{abbr}-{project}-{name}-{env}-{region}", abbreviation = "kv" } }
                });
            using var previewResponse = await client.SendAsync(preview);
            Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
            using var previewContent = await JsonDocument.ParseAsync(await previewResponse.Content.ReadAsStreamAsync());
            var item = Assert.Single(
                previewContent.RootElement.GetProperty("items").EnumerateArray(),
                value => value.GetProperty("resourceId").GetGuid() == resourceId);
            Assert.Equal("vault-res-", item.GetProperty("resourceName").GetString()![..10]);
            Assert.Equal("ComponentType", item.GetProperty("explanation").GetProperty("level").GetString());
            Assert.Contains(
                previewContent.RootElement.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("code").GetString() == "VAL-NOM-LONGUEUR"
                    && issue.GetProperty("resourceId").GetGuid() == resourceId);

            var componentVersionAfterPreview = await GetComponentVersionFromApiAsync(componentId);
            using var clearComponentNaming = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/naming",
                new { defaultTemplate = (string?)null, typeOverrides = Array.Empty<object>(), expectedVersion = componentVersionAfterPreview });
            clearComponentNaming.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var clearComponentNamingResponse = await client.SendAsync(clearComponentNaming);
            Assert.Equal(HttpStatusCode.OK, clearComponentNamingResponse.StatusCode);

            using var normalizedPreview = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/naming/preview",
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = new[] { new { type = " KeyVault ", template = "{abbr}-{project}-{name}-{env}", abbreviation = "vault" } }
                });
            using var normalizedPreviewResponse = await client.SendAsync(normalizedPreview);
            Assert.Equal(HttpStatusCode.OK, normalizedPreviewResponse.StatusCode);
            using var normalizedPreviewContent = await JsonDocument.ParseAsync(await normalizedPreviewResponse.Content.ReadAsStreamAsync());
            var normalizedItem = Assert.Single(
                normalizedPreviewContent.RootElement.GetProperty("items").EnumerateArray(),
                value => value.GetProperty("resourceId").GetGuid() == resourceId);
            Assert.Equal("vault-shop-main-dev", normalizedItem.GetProperty("resourceName").GetString());
        }
        finally
        {
            using var restoreNaming = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/naming",
                new
                {
                    preset = originalSettings.GetProperty("preset").GetString(),
                    defaultTemplate = originalSettings.GetProperty("defaultTemplate").GetString(),
                    resourceGroupTemplate = originalSettings.GetProperty("resourceGroupTemplate").GetString(),
                    typeOverrides = originalSettings.GetProperty("typeOverrides").EnumerateArray()
                        .Select(value => new
                        {
                            type = value.GetProperty("type").GetString(),
                            template = value.GetProperty("template").ValueKind == JsonValueKind.Null
                                ? null
                                : value.GetProperty("template").GetString(),
                            abbreviation = value.GetProperty("abbreviation").ValueKind == JsonValueKind.Null
                                ? null
                                : value.GetProperty("abbreviation").GetString()
                        }).ToArray(),
                    expectedVersion = await GetProjectVersionFromApiAsync()
                });
            restoreNaming.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var restoreResponse = await client.SendAsync(restoreNaming);
            Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        }
    }

    private async Task EffectiveValuesExposeTargetPresenceTagsAndPropertyProvenanceCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var environmentTags = new Dictionary<string, string> { ["environment-level"] = "loaded" };
        using var updateEnvironment = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/environments/{environment.Id:D}",
            new UpdateProjectEnvironmentRequest(
                environment.Name,
                environment.Code,
                environment.SubscriptionId,
                environment.Region,
                environment.IsProtected,
                environment.Approvers,
                environment.Runners,
                environment.Description,
                environmentTags,
                false,
                await GetProjectVersionFromApiAsync(),
                environment.DeploymentConnection));
        updateEnvironment.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var environmentResponse = await client.SendAsync(updateEnvironment);
        Assert.True(environmentResponse.StatusCode == HttpStatusCode.OK, await environmentResponse.Content.ReadAsStringAsync());

        var componentId = await CreateResourceTestComponentAsync(environment);
        var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
        var resourceBody = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "KeyVault",
            $"kv-{Guid.NewGuid():N}"[..12],
            new Dictionary<string, object?> { ["purgeProtection"] = false, ["sku"] = "premium" });
        using var resource = JsonDocument.Parse(resourceBody);
        var resourceId = resource.RootElement.GetProperty("id").GetGuid();
        var resourceVersion = resource.RootElement.GetProperty("version").GetInt32();

        using var setOverride = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/overrides",
            new
            {
                targetId = environment.Id,
                property = "purgeProtection",
                value = true,
                expectedVersion = resourceVersion
            });
        setOverride.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var overrideResponse = await client.SendAsync(setOverride);
        Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{ProjectId:D}/resources/{resourceId:D}/effective-values");
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var targets = result.RootElement.EnumerateArray().ToArray();
        var target = Assert.Single(targets);
        Assert.Equal(environment.Id, target.GetProperty("id").GetGuid());
        Assert.Equal(environment.Code, target.GetProperty("code").GetString());
        Assert.True(target.GetProperty("isPresent").GetBoolean());
        Assert.Equal(environment.Region, target.GetProperty("region").GetString());
        Assert.Equal("shop", target.GetProperty("tags").GetProperty("ifs-project").GetString());
        Assert.Equal("loaded", target.GetProperty("tags").GetProperty("environment-level").GetString());

        var properties = target.GetProperty("properties").EnumerateArray().ToArray();
        var purgeProtection = Assert.Single(properties, value => value.GetProperty("name").GetString() == "purgeProtection");
        Assert.True(purgeProtection.GetProperty("value").GetBoolean());
        Assert.Equal("override", purgeProtection.GetProperty("provenance").GetString());

        var sku = Assert.Single(properties, value => value.GetProperty("name").GetString() == "sku");
        Assert.Equal("premium", sku.GetProperty("value").GetString());
        Assert.Equal("resource", sku.GetProperty("provenance").GetString());

        var retention = Assert.Single(properties, value => value.GetProperty("name").GetString() == "softDeleteRetentionInDays");
        Assert.Equal(90, retention.GetProperty("value").GetInt32());
        Assert.Equal("catalogDefault", retention.GetProperty("provenance").GetString());

        var logWorkspaceBody = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "LogAnalyticsWorkspace",
            $"log-{Guid.NewGuid():N}"[..12],
            new Dictionary<string, object?>());
        using var logWorkspace = JsonDocument.Parse(logWorkspaceBody);
        var logWorkspaceId = logWorkspace.RootElement.GetProperty("id").GetGuid();
        using var logRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{ProjectId:D}/resources/{logWorkspaceId:D}/effective-values");
        using var logResponse = await client.SendAsync(logRequest);
        Assert.Equal(HttpStatusCode.OK, logResponse.StatusCode);
        using var logResult = await JsonDocument.ParseAsync(await logResponse.Content.ReadAsStreamAsync());
        var logTarget = Assert.Single(logResult.RootElement.EnumerateArray());
        var dailyQuota = Assert.Single(
            logTarget.GetProperty("properties").EnumerateArray(),
            value => value.GetProperty("name").GetString() == "dailyQuotaGb");
        Assert.Equal(JsonValueKind.Null, dailyQuota.GetProperty("value").ValueKind);
        Assert.Equal("absent", dailyQuota.GetProperty("provenance").GetString());

        var existingResourceBody = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "KeyVault",
            $"kv-{Guid.NewGuid():N}"[..12],
            new Dictionary<string, object?>(),
            isExisting: true);
        using var existingResource = JsonDocument.Parse(existingResourceBody);
        var existingResourceId = existingResource.RootElement.GetProperty("id").GetGuid();
        using var existingRequest = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{ProjectId:D}/resources/{existingResourceId:D}/effective-values");
        using var existingResponse = await client.SendAsync(existingRequest);
        Assert.Equal(HttpStatusCode.OK, existingResponse.StatusCode);
        using var existingResult = await JsonDocument.ParseAsync(await existingResponse.Content.ReadAsStreamAsync());
        var existingTarget = Assert.Single(existingResult.RootElement.EnumerateArray());
        Assert.False(existingTarget.GetProperty("isPresent").GetBoolean());
        Assert.Empty(existingTarget.GetProperty("properties").EnumerateArray());
    }

    private async Task ResourceDeletionImpactPreviewIsServerCalculatedAndReportsPartialCoverageCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var resourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "SqlServer",
                $"sql-{Guid.NewGuid():N}"[..10],
                new Dictionary<string, object?>());
            using var resource = JsonDocument.Parse(resourceBody);
            var resourceId = resource.RootElement.GetProperty("id").GetGuid();
            var initialVersion = resource.RootElement.GetProperty("version").GetInt32();

            using var addChild = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/children",
                new
                {
                    type = "SqlDatabase",
                    name = "db1",
                    properties = new Dictionary<string, object?> { ["maxSizeGb"] = 64 },
                    expectedVersion = initialVersion
                });
            addChild.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addResponse = await client.SendAsync(addChild);
            Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
            using var updatedResource = await JsonDocument.ParseAsync(await addResponse.Content.ReadAsStreamAsync());
            var currentVersion = updatedResource.RootElement.GetProperty("version").GetInt32();

            using var request = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/delete-impact");
            using var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal(currentVersion, result.RootElement.GetProperty("resourceVersion").GetInt32());
            Assert.Equal("Partial", result.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                result.RootElement.GetProperty("pendingAreas").EnumerateArray(),
                area => area.GetString() == "ImplicitEffects");
            Assert.DoesNotContain(
                result.RootElement.GetProperty("pendingAreas").EnumerateArray(),
                area => area.GetString() == "ApplicationParameters");
            var child = Assert.Single(result.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("Child", child.GetProperty("kind").GetString());
            Assert.Equal("SqlDatabase", child.GetProperty("type").GetString());
            Assert.Equal("db1", child.GetProperty("name").GetString());
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ResourceLinksCanBeCreatedListedAndPreviewedBeforeRemovalCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var targetBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerAppsEnvironment",
                "environment",
                new Dictionary<string, object?>());
            using var target = JsonDocument.Parse(targetBody);
            var targetResourceId = target.RootElement.GetProperty("id").GetGuid();

            var replacementTargetBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerAppsEnvironment",
                "replacement-environment",
                new Dictionary<string, object?>());
            using var replacementTarget = JsonDocument.Parse(replacementTargetBody);
            var replacementTargetResourceId = replacementTarget.RootElement.GetProperty("id").GetGuid();

            var sourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "application",
                new Dictionary<string, object?>());
            using var source = JsonDocument.Parse(sourceBody);
            var sourceResourceId = source.RootElement.GetProperty("id").GetGuid();

            var otherSourceBody = await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "other-application",
                new Dictionary<string, object?>());
            using var otherSource = JsonDocument.Parse(otherSourceBody);
            var otherSourceResourceId = otherSource.RootElement.GetProperty("id").GetGuid();

            var expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId);

            using var addLink = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion
                });
            addLink.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addResponse = await client.SendAsync(addLink);
            Assert.True(addResponse.StatusCode == HttpStatusCode.Created, await addResponse.Content.ReadAsStringAsync());
            using var created = await JsonDocument.ParseAsync(await addResponse.Content.ReadAsStreamAsync());
            var linkId = created.RootElement.GetProperty("linkId").GetGuid();
            Assert.True(await GetComponentVersionFromApiAsync(componentId) > expectedComponentVersion);

            using var unsupportedRolesRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{otherSourceResourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = replacementTargetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = ReaderRoleNames,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            unsupportedRolesRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var unsupportedRolesResponse = await client.SendAsync(unsupportedRolesRequest);
            Assert.Equal(HttpStatusCode.BadRequest, unsupportedRolesResponse.StatusCode);
            Assert.Equal("LINK_ROLE_UNSUPPORTED", await GetProblemCodeAsync(unsupportedRolesResponse));

            using var invalidLevelRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{otherSourceResourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = replacementTargetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = "Schema",
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            invalidLevelRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var invalidLevelResponse = await client.SendAsync(invalidLevelRequest);
            Assert.Equal(HttpStatusCode.BadRequest, invalidLevelResponse.StatusCode);
            Assert.Equal("LINK_DATA_ACCESS_LEVEL_UNSUPPORTED", await GetProblemCodeAsync(invalidLevelResponse));

            var unsupportedLink = LinkAggregate.Create(
                Guid.NewGuid(),
                new ProjectId(ProjectId),
                new OrganizationId(OrganizationId),
                componentId,
                sourceResourceId,
                componentId,
                targetResourceId,
                LinkKind.Access,
                LinkIdentityKind.None,
                null,
                [],
                LinkAccessScope.Resource,
                null);
            Assert.False(unsupportedLink.IsError);
            using (var scope = factory.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
                var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
                dbContext.Set<LinkAggregate>().Add(unsupportedLink.Value);
                await dbContext.SaveChangesAsync();
            }

            using var sourceLinksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links");
            using var sourceLinksResponse = await client.SendAsync(sourceLinksRequest);
            Assert.Equal(HttpStatusCode.OK, sourceLinksResponse.StatusCode);
            using var sourceLinks = await JsonDocument.ParseAsync(await sourceLinksResponse.Content.ReadAsStreamAsync());
            var outgoingLinks = sourceLinks.RootElement.GetProperty("outgoing").EnumerateArray().ToArray();
            Assert.Equal(2, outgoingLinks.Length);
            var outgoing = Assert.Single(outgoingLinks, link => link.GetProperty("id").GetGuid() == linkId);
            Assert.Equal("Hosting", outgoing.GetProperty("kind").GetString());
            Assert.Equal("Explicit", outgoing.GetProperty("origin").GetString());
            Assert.Equal(targetResourceId, outgoing.GetProperty("targetResourceId").GetGuid());
            Assert.True(outgoing.GetProperty("createsDeploymentDependency").GetBoolean());
            Assert.Empty(outgoing.GetProperty("findings").EnumerateArray());
            var unsupported = Assert.Single(outgoingLinks, link => link.GetProperty("id").GetGuid() != linkId);
            Assert.Contains(
                unsupported.GetProperty("findings").EnumerateArray(),
                finding => finding.GetProperty("code").GetString() == "VAL-LIA-TYPE");

            using var wrongSourceUpdate = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{otherSourceResourceId:D}/links/{linkId:D}",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = replacementTargetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedLinkVersion = 1
                });
            wrongSourceUpdate.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var wrongSourceUpdateResponse = await client.SendAsync(wrongSourceUpdate);
            Assert.Equal(HttpStatusCode.NotFound, wrongSourceUpdateResponse.StatusCode);

            using var updateLink = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links/{linkId:D}",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = replacementTargetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedLinkVersion = 1
                });
            updateLink.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var updateResponse = await client.SendAsync(updateLink);
            Assert.True(updateResponse.StatusCode == HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());

            using var targetLinksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{replacementTargetResourceId:D}/links");
            using var targetLinksResponse = await client.SendAsync(targetLinksRequest);
            Assert.Equal(HttpStatusCode.OK, targetLinksResponse.StatusCode);
            using var targetLinks = await JsonDocument.ParseAsync(await targetLinksResponse.Content.ReadAsStreamAsync());
            var incoming = Assert.Single(targetLinks.RootElement.GetProperty("incoming").EnumerateArray());
            Assert.Equal(linkId, incoming.GetProperty("id").GetGuid());
            Assert.Equal(sourceResourceId, incoming.GetProperty("sourceResourceId").GetGuid());

            using var impactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links/{linkId:D}/impact");
            using var impactResponse = await client.SendAsync(impactRequest);
            Assert.Equal(HttpStatusCode.OK, impactResponse.StatusCode);
            using var impact = await JsonDocument.ParseAsync(await impactResponse.Content.ReadAsStreamAsync());
            Assert.Equal("Blocker", impact.RootElement.GetProperty("severity").GetString());
            Assert.Contains(
                impact.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("code").GetString() == "IMPACT-LIA-OBLIGATOIRE-ROMPU");
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ResourceLinkUpdateTrimsRolesBeforeCalculatingImpactCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            using var identity = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "UserAssignedIdentity", "role-normalization-identity", new Dictionary<string, object?>()));
            using var source = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerApp", "role-normalization-source", new Dictionary<string, object?>()));
            using var target = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "KeyVault", "role-normalization-target", new Dictionary<string, object?>()));
            var identityId = identity.RootElement.GetProperty("id").GetGuid();
            var sourceId = source.RootElement.GetProperty("id").GetGuid();
            var targetId = target.RootElement.GetProperty("id").GetGuid();

            using var addLinkRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = KeyVaultSecretUserRoles,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            addLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addLinkResponse = await client.SendAsync(addLinkRequest);
            Assert.True(addLinkResponse.StatusCode == HttpStatusCode.Created, await addLinkResponse.Content.ReadAsStringAsync());
            using var created = await JsonDocument.ParseAsync(await addLinkResponse.Content.ReadAsStreamAsync());
            var linkId = created.RootElement.GetProperty("linkId").GetGuid();

            using var updateLinkRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links/{linkId:D}",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = WhitespaceKeyVaultSecretUserRoles,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedLinkVersion = 1
                });
            updateLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var updateLinkResponse = await client.SendAsync(updateLinkRequest);
            Assert.True(updateLinkResponse.StatusCode == HttpStatusCode.OK, await updateLinkResponse.Content.ReadAsStringAsync());
            using var updated = await JsonDocument.ParseAsync(await updateLinkResponse.Content.ReadAsStreamAsync());
            Assert.DoesNotContain(updated.RootElement.GetProperty("findings").EnumerateArray(), finding =>
                finding.GetString()!.Contains("révoqué", StringComparison.OrdinalIgnoreCase));

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            var outgoing = Assert.Single(links.RootElement.GetProperty("outgoing").EnumerateArray());
            Assert.Equal("Key Vault Secrets User", Assert.Single(outgoing.GetProperty("roles").EnumerateArray()).GetString());
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ResourceLinkCanBeRemovedAfterReviewingImpactCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            using var identity = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "UserAssignedIdentity", "removal-identity", new Dictionary<string, object?>()));
            using var source = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerApp", "removal-source", new Dictionary<string, object?>()));
            using var target = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "KeyVault", "removal-target", new Dictionary<string, object?>()));
            var identityId = identity.RootElement.GetProperty("id").GetGuid();
            var sourceId = source.RootElement.GetProperty("id").GetGuid();
            var targetId = target.RootElement.GetProperty("id").GetGuid();

            using var addLinkRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = KeyVaultSecretUserRoles,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            addLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addLinkResponse = await client.SendAsync(addLinkRequest);
            Assert.True(addLinkResponse.StatusCode == HttpStatusCode.Created, await addLinkResponse.Content.ReadAsStringAsync());
            using var created = await JsonDocument.ParseAsync(await addLinkResponse.Content.ReadAsStreamAsync());
            var linkId = created.RootElement.GetProperty("linkId").GetGuid();

            using var impactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links/{linkId:D}/impact");
            using var impactResponse = await client.SendAsync(impactRequest);
            Assert.Equal(HttpStatusCode.OK, impactResponse.StatusCode);
            using var impact = await JsonDocument.ParseAsync(await impactResponse.Content.ReadAsStreamAsync());
            Assert.Equal("Critical", impact.RootElement.GetProperty("severity").GetString());
            Assert.Contains(impact.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("code").GetString() == "IMPACT-LIA-ROLE-REVOKED");

            using var removeLinkRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links/{linkId:D}",
                new { expectedLinkVersion = 1 });
            removeLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var removeLinkResponse = await client.SendAsync(removeLinkRequest);
            Assert.Equal(HttpStatusCode.OK, removeLinkResponse.StatusCode);
            using var removed = await JsonDocument.ParseAsync(await removeLinkResponse.Content.ReadAsStreamAsync());
            Assert.Contains(removed.RootElement.GetProperty("findings").EnumerateArray(), finding =>
                finding.GetString()!.Contains("révoqué", StringComparison.OrdinalIgnoreCase));

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            Assert.DoesNotContain(links.RootElement.GetProperty("outgoing").EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == linkId);
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task StaleComponentVersionWinsOverLinkCardinalityValidationCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            using var firstTarget = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerAppsEnvironment", "stale-first-environment", new Dictionary<string, object?>()));
            using var secondTarget = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerAppsEnvironment", "stale-second-environment", new Dictionary<string, object?>()));
            using var source = JsonDocument.Parse(await CreateResourceAsync(
                componentId, resourceGroupId, "ContainerApp", "stale-version-application", new Dictionary<string, object?>()));
            var sourceId = source.RootElement.GetProperty("id").GetGuid();
            var firstTargetId = firstTarget.RootElement.GetProperty("id").GetGuid();
            var secondTargetId = secondTarget.RootElement.GetProperty("id").GetGuid();
            var initialVersion = await GetComponentVersionFromApiAsync(componentId);

            HttpRequestMessage CreateHostingRequest(Guid targetId, int expectedVersion)
            {
                var request = CreateRequest(
                    HttpMethod.Post,
                    $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                    new
                    {
                        targetComponentId = componentId,
                        targetResourceId = targetId,
                        kind = "Hosting",
                        identityKind = "None",
                        identityResourceId = (Guid?)null,
                        roles = Array.Empty<string>(),
                        accessScope = "Resource",
                        dataAccessLevel = (string?)null,
                        expectedComponentVersion = expectedVersion
                    });
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                return request;
            }

            using var firstRequest = CreateHostingRequest(firstTargetId, initialVersion);
            using var firstResponse = await client.SendAsync(firstRequest);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            using var staleRequest = CreateHostingRequest(secondTargetId, initialVersion);
            using var staleResponse = await client.SendAsync(staleRequest);
            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
            Assert.Equal("CONFLICT_VERSION", await GetProblemCodeAsync(staleResponse));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task ConcurrentHostingLinksRespectComponentVersionAndCardinalityCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            var firstTargetBody = await CreateResourceAsync(componentId, resourceGroupId,
                "ContainerAppsEnvironment", "first-environment", new Dictionary<string, object?>());
            var secondTargetBody = await CreateResourceAsync(componentId, resourceGroupId,
                "ContainerAppsEnvironment", "second-environment", new Dictionary<string, object?>());
            var sourceBody = await CreateResourceAsync(componentId, resourceGroupId,
                "ContainerApp", "concurrent-application", new Dictionary<string, object?>());
            using var firstTarget = JsonDocument.Parse(firstTargetBody);
            using var secondTarget = JsonDocument.Parse(secondTargetBody);
            using var source = JsonDocument.Parse(sourceBody);
            var sourceResourceId = source.RootElement.GetProperty("id").GetGuid();
            var expectedVersion = await GetComponentVersionFromApiAsync(componentId);

            HttpRequestMessage CreateLinkRequest(Guid targetResourceId, int componentVersion = -1)
            {
                var request = CreateRequest(
                    HttpMethod.Post,
                    $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links",
                    new
                    {
                        targetComponentId = componentId,
                        targetResourceId,
                        kind = "Hosting",
                        identityKind = "None",
                        identityResourceId = (Guid?)null,
                        roles = Array.Empty<string>(),
                        accessScope = "Resource",
                        dataAccessLevel = (string?)null,
                        expectedComponentVersion = componentVersion < 0 ? expectedVersion : componentVersion
                    });
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                return request;
            }

            using var firstRequest = CreateLinkRequest(firstTarget.RootElement.GetProperty("id").GetGuid());
            using var secondRequest = CreateLinkRequest(secondTarget.RootElement.GetProperty("id").GetGuid());
            var responses = await Task.WhenAll(client.SendAsync(firstRequest), client.SendAsync(secondRequest));
            using var firstResponse = responses[0];
            using var secondResponse = responses[1];
            var successfulCount = responses.Count(response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(1, successfulCount);
            var rejected = Assert.Single(responses, response => response.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceResourceId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            var existingLink = Assert.Single(links.RootElement.GetProperty("outgoing").EnumerateArray());
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));

            var linkedTargetId = existingLink.GetProperty("targetResourceId").GetGuid();
            var unlinkedTargetId = firstTarget.RootElement.GetProperty("id").GetGuid() == linkedTargetId
                ? secondTarget.RootElement.GetProperty("id").GetGuid()
                : firstTarget.RootElement.GetProperty("id").GetGuid();
            using var cardinalityRequest = CreateLinkRequest(
                unlinkedTargetId,
                await GetComponentVersionFromApiAsync(componentId));
            using var cardinalityResponse = await client.SendAsync(cardinalityRequest);
            Assert.Equal(HttpStatusCode.BadRequest, cardinalityResponse.StatusCode);
            Assert.Equal("LINK_CARDINALITY_EXCEEDED", await GetProblemCodeAsync(cardinalityResponse));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task DefaultLogAnalyticsRejectsIncompatibleTargetComponentsCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var targetComponentId = await CreateResourceTestComponentAsync(environment);
        Guid? singleComponentId = null;
        Guid? allEnvironmentComponentId = null;
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(targetComponentId);
            using var workspace = JsonDocument.Parse(await CreateResourceAsync(
                targetComponentId,
                resourceGroupId,
                "LogAnalyticsWorkspace",
                "default-workspace",
                new Dictionary<string, object?>()));
            var workspaceId = workspace.RootElement.GetProperty("id").GetGuid();

            var singleSuffix = Guid.NewGuid().ToString("N")[..8];
            using var singleComponentResponse = await SendCreateComponentAsync(new
            {
                name = $"Single {singleSuffix}",
                code = $"single-{singleSuffix}",
                description = "Single source for Log Analytics compatibility test",
                mode = "Single",
                targetsAllEnvironments = false,
                targetEnvironmentIds = Array.Empty<Guid>(),
                singleTarget = new
                {
                    code = $"s{singleSuffix[..4]}",
                    subscriptionId = Guid.NewGuid(),
                    region = "francecentral",
                    deploymentConnection = (string?)null,
                    isProtected = false,
                    approvers = Array.Empty<string>(),
                    runners = Array.Empty<string>()
                },
                removedResourceBehavior = "Detach",
                deletionProtectionEnabled = true,
                tags = new Dictionary<string, string>(),
                expectedVersion = await GetProjectVersionFromApiAsync()
            });
            Assert.Equal(HttpStatusCode.Created, singleComponentResponse.StatusCode);
            using var singleComponent = await JsonDocument.ParseAsync(await singleComponentResponse.Content.ReadAsStreamAsync());
            singleComponentId = singleComponent.RootElement.GetProperty("id").GetGuid();

            using var singleDefaultRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{singleComponentId:D}/default-log-analytics",
                new
                {
                    workspaceResourceId = workspaceId,
                    expectedVersion = await GetComponentVersionFromApiAsync(singleComponentId.Value)
                });
            singleDefaultRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var singleDefaultResponse = await client.SendAsync(singleDefaultRequest);
            Assert.Equal(HttpStatusCode.BadRequest, singleDefaultResponse.StatusCode);
            Assert.Equal("VAL-CMP-SENS", await GetProblemCodeAsync(singleDefaultResponse));

            var allEnvironmentsSuffix = Guid.NewGuid().ToString("N")[..8];
            using var allEnvironmentsResponse = await SendCreateComponentAsync(new
            {
                name = $"All environments {allEnvironmentsSuffix}",
                code = $"all-{allEnvironmentsSuffix}",
                description = "Per-environment source for Log Analytics compatibility test",
                mode = "PerEnvironment",
                targetsAllEnvironments = true,
                targetEnvironmentIds = Array.Empty<Guid>(),
                singleTarget = (object?)null,
                removedResourceBehavior = "Detach",
                deletionProtectionEnabled = true,
                tags = new Dictionary<string, string>(),
                expectedVersion = await GetProjectVersionFromApiAsync()
            });
            Assert.Equal(HttpStatusCode.Created, allEnvironmentsResponse.StatusCode);
            using var allEnvironmentsComponent = await JsonDocument.ParseAsync(await allEnvironmentsResponse.Content.ReadAsStreamAsync());
            allEnvironmentComponentId = allEnvironmentsComponent.RootElement.GetProperty("id").GetGuid();

            using var allEnvironmentDefaultRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{allEnvironmentComponentId:D}/default-log-analytics",
                new
                {
                    workspaceResourceId = workspaceId,
                    expectedVersion = await GetComponentVersionFromApiAsync(allEnvironmentComponentId.Value)
                });
            allEnvironmentDefaultRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var allEnvironmentDefaultResponse = await client.SendAsync(allEnvironmentDefaultRequest);
            Assert.Equal(HttpStatusCode.BadRequest, allEnvironmentDefaultResponse.StatusCode);
            Assert.Equal("VAL-CMP-ENVIRONNEMENTS", await GetProblemCodeAsync(allEnvironmentDefaultResponse));
        }
        finally
        {
            if (singleComponentId is { } singleId)
            {
                await CleanupResourceTestComponentAsync(singleId);
            }

            if (allEnvironmentComponentId is { } allEnvironmentsId)
            {
                await CleanupResourceTestComponentAsync(allEnvironmentsId);
            }

            await CleanupResourceTestComponentAsync(targetComponentId);
        }
    }

    private async Task DeletingDefaultLogAnalyticsWorkspaceClearsComponentReferenceCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var sourceComponentId = await CreateResourceTestComponentAsync(environment);
        var workspaceComponentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var sourceGroupId = await CreateResourceTestGroupAsync(sourceComponentId);
            var workspaceGroupId = await CreateResourceTestGroupAsync(workspaceComponentId);
            var workspaceBody = await CreateResourceAsync(
                workspaceComponentId,
                workspaceGroupId,
                "LogAnalyticsWorkspace",
                "default-workspace",
                new Dictionary<string, object?>());
            using var workspace = JsonDocument.Parse(workspaceBody);
            var workspaceId = workspace.RootElement.GetProperty("id").GetGuid();
            var resourceVersion = workspace.RootElement.GetProperty("version").GetInt32();
            var sourceBody = await CreateResourceAsync(
                sourceComponentId,
                sourceGroupId,
                "KeyVault",
                "diagnostic-source",
                new Dictionary<string, object?>());
            using var source = JsonDocument.Parse(sourceBody);
            var sourceResourceId = source.RootElement.GetProperty("id").GetGuid();

            using var setDefaultRequest = CreateRequest(
                HttpMethod.Put,
                $"/v1/projects/{ProjectId:D}/components/{sourceComponentId:D}/default-log-analytics",
                new
                {
                    workspaceResourceId = workspaceId,
                    expectedVersion = await GetComponentVersionFromApiAsync(sourceComponentId)
                });
            setDefaultRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var setDefaultResponse = await client.SendAsync(setDefaultRequest);
            Assert.True(setDefaultResponse.StatusCode == HttpStatusCode.OK, await setDefaultResponse.Content.ReadAsStringAsync());
            using var componentResponse = await JsonDocument.ParseAsync(await setDefaultResponse.Content.ReadAsStreamAsync());
            Assert.Equal(workspaceId,
                componentResponse.RootElement.GetProperty("defaultLogAnalyticsResourceId").GetGuid());

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{sourceComponentId:D}/resources/{sourceResourceId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            var implicitDiagnostic = Assert.Single(links.RootElement.GetProperty("outgoing").EnumerateArray());
            Assert.Equal("Diagnostics", implicitDiagnostic.GetProperty("kind").GetString());
            Assert.Equal("Implicit", implicitDiagnostic.GetProperty("origin").GetString());
            Assert.Equal(workspaceId, implicitDiagnostic.GetProperty("targetResourceId").GetGuid());

            using var impactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{workspaceComponentId:D}/resources/{workspaceId:D}/delete-impact");
            using var impactResponse = await client.SendAsync(impactRequest);
            Assert.Equal(HttpStatusCode.OK, impactResponse.StatusCode);
            using var impact = await JsonDocument.ParseAsync(await impactResponse.Content.ReadAsStreamAsync());
            Assert.Contains(impact.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("code").GetString() == "IMPACT-LIA-LINK-IMPLICITE-RETIRE"
                && item.GetProperty("resourceId").GetGuid() == sourceResourceId);

            using var deleteRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{workspaceComponentId:D}/resources/{workspaceId:D}",
                new { expectedVersion = resourceVersion });
            deleteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteResponse = await client.SendAsync(deleteRequest);
            Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent, await deleteResponse.Content.ReadAsStringAsync());

            using var checkComponentRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{sourceComponentId:D}");
            using var checkComponentResponse = await client.SendAsync(checkComponentRequest);
            Assert.Equal(HttpStatusCode.OK, checkComponentResponse.StatusCode);
            using var persistedComponent = await JsonDocument.ParseAsync(await checkComponentResponse.Content.ReadAsStreamAsync());
            Assert.Equal(JsonValueKind.Null,
                persistedComponent.RootElement.GetProperty("defaultLogAnalyticsResourceId").ValueKind);
        }
        finally
        {
            await CleanupResourceTestComponentAsync(sourceComponentId);
            await CleanupResourceTestComponentAsync(workspaceComponentId);
        }
    }

    private async Task DeletingUserAssignedIdentityUsedByALinkIsBlockedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
            using var identity = JsonDocument.Parse(await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "UserAssignedIdentity",
                "api-identity",
                new Dictionary<string, object?>()));
            using var source = JsonDocument.Parse(await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>()));
            using var target = JsonDocument.Parse(await CreateResourceAsync(
                componentId,
                resourceGroupId,
                "KeyVault",
                "main-vault",
                new Dictionary<string, object?>()));
            var identityId = identity.RootElement.GetProperty("id").GetGuid();
            var identityVersion = identity.RootElement.GetProperty("version").GetInt32();
            var sourceId = source.RootElement.GetProperty("id").GetGuid();
            var targetId = target.RootElement.GetProperty("id").GetGuid();

            using var addLinkRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = KeyVaultSecretUserRoles,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            addLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addLinkResponse = await client.SendAsync(addLinkRequest);
            Assert.True(addLinkResponse.StatusCode == HttpStatusCode.Created, await addLinkResponse.Content.ReadAsStringAsync());

            using var duplicateLinkRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = DuplicateKeyVaultSecretUserRoles,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            duplicateLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var duplicateLinkResponse = await client.SendAsync(duplicateLinkRequest);
            Assert.Equal(HttpStatusCode.BadRequest, duplicateLinkResponse.StatusCode);
            Assert.Equal("VAL-LIA-DOUBLON", await GetProblemCodeAsync(duplicateLinkResponse));

            using var duplicateRolesRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{sourceId:D}/links",
                new
                {
                    targetComponentId = componentId,
                    targetResourceId = targetId,
                    kind = "Access",
                    identityKind = "UserAssigned",
                    identityResourceId = identityId,
                    roles = DuplicateRoleEntries,
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(componentId)
                });
            duplicateRolesRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var duplicateRolesResponse = await client.SendAsync(duplicateRolesRequest);
            Assert.Equal(HttpStatusCode.BadRequest, duplicateRolesResponse.StatusCode);
            Assert.Equal("LINK_ROLE_DUPLICATE", await GetProblemCodeAsync(duplicateRolesResponse));

            using var impactRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{identityId:D}/delete-impact");
            using var impactResponse = await client.SendAsync(impactRequest);
            Assert.Equal(HttpStatusCode.OK, impactResponse.StatusCode);
            using var impact = await JsonDocument.ParseAsync(await impactResponse.Content.ReadAsStreamAsync());
            Assert.Equal("Blocked", impact.RootElement.GetProperty("status").GetString());
            Assert.Contains(impact.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("code").GetString() == "IMPACT-LIA-IDENTITE-UTILISEE");

            using var deleteRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{identityId:D}",
                new { expectedVersion = identityVersion });
            deleteRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteResponse = await client.SendAsync(deleteRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
            Assert.Equal("RESOURCE_DELETE_BLOCKED", await GetProblemCodeAsync(deleteResponse));
        }
        finally
        {
            await CleanupResourceTestComponentAsync(componentId);
        }
    }

    private async Task DeletingComponentWithIncomingLinksIsBlockedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var sourceComponentId = await CreateResourceTestComponentAsync(environment);
        var targetComponentId = await CreateResourceTestComponentAsync(environment);
        try
        {
            var sourceGroupId = await CreateResourceTestGroupAsync(sourceComponentId);
            var targetGroupId = await CreateResourceTestGroupAsync(targetComponentId);
            using var source = JsonDocument.Parse(await CreateResourceAsync(
                sourceComponentId,
                sourceGroupId,
                "ContainerApp",
                "api",
                new Dictionary<string, object?>()));
            using var target = JsonDocument.Parse(await CreateResourceAsync(
                targetComponentId,
                targetGroupId,
                "ContainerAppsEnvironment",
                "environment",
                new Dictionary<string, object?>()));
            var sourceResourceId = source.RootElement.GetProperty("id").GetGuid();
            var targetResourceId = target.RootElement.GetProperty("id").GetGuid();
            var targetVersionBeforeLink = await GetComponentVersionFromApiAsync(targetComponentId);

            using var addLinkRequest = CreateRequest(
                HttpMethod.Post,
                $"/v1/projects/{ProjectId:D}/components/{sourceComponentId:D}/resources/{sourceResourceId:D}/links",
                new
                {
                    targetComponentId,
                    targetResourceId,
                    kind = "Hosting",
                    identityKind = "None",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = await GetComponentVersionFromApiAsync(sourceComponentId)
                });
            addLinkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var addLinkResponse = await client.SendAsync(addLinkRequest);
            Assert.Equal(HttpStatusCode.Created, addLinkResponse.StatusCode);
            Assert.Equal(targetVersionBeforeLink + 1, await GetComponentVersionFromApiAsync(targetComponentId));

            using var deleteComponentRequest = CreateRequest(
                HttpMethod.Delete,
                $"/v1/projects/{ProjectId:D}/components/{targetComponentId:D}",
                new { expectedVersion = await GetComponentVersionFromApiAsync(targetComponentId) });
            deleteComponentRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            using var deleteComponentResponse = await client.SendAsync(deleteComponentRequest);
            Assert.Equal(HttpStatusCode.BadRequest, deleteComponentResponse.StatusCode);
            using var deleteProblem = JsonDocument.Parse(await deleteComponentResponse.Content.ReadAsStringAsync());
            Assert.Equal("COMPONENT_DELETE_BLOCKED", deleteProblem.RootElement.GetProperty("code").GetString());
            Assert.Contains(deleteProblem.RootElement.GetProperty("affectedResources").EnumerateArray(),
                resourceId => resourceId.GetString() == sourceResourceId.ToString("D"));

            using var linksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{sourceComponentId:D}/resources/{sourceResourceId:D}/links");
            using var linksResponse = await client.SendAsync(linksRequest);
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            using var links = await JsonDocument.ParseAsync(await linksResponse.Content.ReadAsStreamAsync());
            Assert.Single(links.RootElement.GetProperty("outgoing").EnumerateArray());

            using var deletedComponentRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{targetComponentId:D}");
            using var deletedComponentResponse = await client.SendAsync(deletedComponentRequest);
            Assert.Equal(HttpStatusCode.OK, deletedComponentResponse.StatusCode);

            using var deletedResourceLinksRequest = CreateRequest(
                HttpMethod.Get,
                $"/v1/projects/{ProjectId:D}/components/{targetComponentId:D}/resources/{targetResourceId:D}/links");
            using var deletedResourceLinksResponse = await client.SendAsync(deletedResourceLinksRequest);
            Assert.Equal(HttpStatusCode.OK, deletedResourceLinksResponse.StatusCode);
        }
        finally
        {
            await CleanupResourceTestComponentAsync(sourceComponentId);
            await CleanupResourceTestComponentAsync(targetComponentId);
        }
    }

    private async Task SingleTargetResourceUsesThePersistedSingleTargetIdCore()
    {
        await PrepareAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var createComponent = await SendCreateComponentAsync(new
        {
            name = $"Platform {suffix}",
            code = $"plat-{suffix}",
            description = "Single target resource test",
            mode = "Single",
            targetsAllEnvironments = false,
            targetEnvironmentIds = Array.Empty<Guid>(),
            singleTarget = new
            {
                code = $"s{suffix[..4]}",
                subscriptionId = Guid.NewGuid(),
                region = "francecentral",
                deploymentConnection = (string?)null,
                isProtected = true,
                approvers = AliceApprovers,
                runners = Array.Empty<string>()
            },
            removedResourceBehavior = "Detach",
            deletionProtectionEnabled = true,
            tags = new Dictionary<string, string>(),
            expectedVersion = await GetProjectVersionFromApiAsync()
        });
        Assert.Equal(HttpStatusCode.Created, createComponent.StatusCode);
        using var createdComponent = await JsonDocument.ParseAsync(await createComponent.Content.ReadAsStreamAsync());
        var componentId = createdComponent.RootElement.GetProperty("id").GetGuid();
        var targetId = createdComponent.RootElement.GetProperty("singleTarget").GetProperty("id").GetGuid();
        var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
        var resourceBody = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "KeyVault",
            $"kv-{suffix}",
            new Dictionary<string, object?>(),
            isExisting: true);
        using var createdResource = JsonDocument.Parse(resourceBody);
        var resourceId = createdResource.RootElement.GetProperty("id").GetGuid();
        var resourceVersion = createdResource.RootElement.GetProperty("version").GetInt32();
        var azureResourceId = $"/subscriptions/{Guid.NewGuid():D}/resourceGroups/rg-{suffix}/providers/Microsoft.KeyVault/vaults/kv-{suffix}";

        using var setExistingId = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/existing-id",
            new { targetId, resourceId = azureResourceId, expectedVersion = resourceVersion });
        setExistingId.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var existingIdResponse = await client.SendAsync(setExistingId);
        Assert.True(existingIdResponse.StatusCode == HttpStatusCode.OK, await existingIdResponse.Content.ReadAsStringAsync());
        using var existingResource = await JsonDocument.ParseAsync(await existingIdResponse.Content.ReadAsStreamAsync());
        var idAddedVersion = existingResource.RootElement.GetProperty("version").GetInt32();

        using var setPresence = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/presence",
            new { targetId, isPresent = true, expectedVersion = idAddedVersion });
        setPresence.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var presenceResponse = await client.SendAsync(setPresence);
        Assert.Equal(HttpStatusCode.OK, presenceResponse.StatusCode);
        using var presentResource = await JsonDocument.ParseAsync(await presenceResponse.Content.ReadAsStreamAsync());

        Assert.Equal(azureResourceId, presentResource.RootElement.GetProperty("existingIds").GetProperty(targetId.ToString("D")).GetString());
        Assert.True(presentResource.RootElement.GetProperty("presences").GetProperty(targetId.ToString("D")).GetBoolean());
    }

    private async Task ResourceChildCanBeAddedUpdatedAndRemovedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
        var resourceBody = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "SqlServer",
            $"sql-{Guid.NewGuid():N}"[..10],
            new Dictionary<string, object?>());
        using var resource = JsonDocument.Parse(resourceBody);
        var resourceId = resource.RootElement.GetProperty("id").GetGuid();
        var resourceVersion = resource.RootElement.GetProperty("version").GetInt32();

        using var addChild = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/children",
            new
            {
                type = "SqlDatabase",
                name = "db1",
                properties = new Dictionary<string, object?> { ["maxSizeGb"] = 64 },
                expectedVersion = resourceVersion
            });
        addChild.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var addResponse = await client.SendAsync(addChild);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        using var addedResource = await JsonDocument.ParseAsync(await addResponse.Content.ReadAsStreamAsync());
        var childId = addedResource.RootElement.GetProperty("children")[0].GetProperty("id").GetGuid();
        var addedVersion = addedResource.RootElement.GetProperty("version").GetInt32();
        Assert.Equal(64, addedResource.RootElement.GetProperty("children")[0].GetProperty("properties").GetProperty("maxSizeGb").GetInt32());

        using var updateChild = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/children/{childId:D}",
            new
            {
                type = "SqlDatabase",
                name = "db2",
                properties = new Dictionary<string, object?> { ["maxSizeGb"] = 96 },
                expectedVersion = addedVersion
            });
        updateChild.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var updateResponse = await client.SendAsync(updateChild);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updatedResource = await JsonDocument.ParseAsync(await updateResponse.Content.ReadAsStreamAsync());
        var updatedVersion = updatedResource.RootElement.GetProperty("version").GetInt32();
        Assert.Equal("db2", updatedResource.RootElement.GetProperty("children")[0].GetProperty("name").GetString());
        Assert.Equal(96, updatedResource.RootElement.GetProperty("children")[0].GetProperty("properties").GetProperty("maxSizeGb").GetInt32());

        using var removeChild = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{resourceId:D}/children/{childId:D}",
            new { expectedVersion = updatedVersion });
        removeChild.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var removeResponse = await client.SendAsync(removeChild);

        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        using var finalResource = await JsonDocument.ParseAsync(await removeResponse.Content.ReadAsStreamAsync());
        Assert.Empty(finalResource.RootElement.GetProperty("children").EnumerateArray());
    }

    private async Task ResourceValuesRoundTripAndIrreversibleOverrideCannotBeReversedCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var componentId = await CreateResourceTestComponentAsync(environment);
        var resourceGroupId = await CreateResourceTestGroupAsync(componentId);
        var nestedProperties = new Dictionary<string, object?>
        {
            ["workloadProfiles"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["name"] = "D4",
                    ["limits"] = new Dictionary<string, object?> { ["cpu"] = 1.5m },
                    ["labels"] = new[] { "api", "worker" }
                }
            }
        };
        var nestedResource = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "ContainerAppsEnvironment",
            $"cae-{Guid.NewGuid():N}"[..12],
            nestedProperties);
        using (var nested = JsonDocument.Parse(nestedResource))
        {
            var workloadProfile = nested.RootElement.GetProperty("properties").GetProperty("workloadProfiles")[0];
            Assert.Equal("D4", workloadProfile.GetProperty("name").GetString());
            Assert.Equal(1.5m, workloadProfile.GetProperty("limits").GetProperty("cpu").GetDecimal());
            Assert.Equal("worker", workloadProfile.GetProperty("labels")[1].GetString());
        }

        var keyVaultProperties = new Dictionary<string, object?> { ["purgeProtection"] = false };
        var keyVaultResource = await CreateResourceAsync(
            componentId,
            resourceGroupId,
            "KeyVault",
            $"kv-{Guid.NewGuid():N}"[..12],
            keyVaultProperties);
        using var keyVault = JsonDocument.Parse(keyVaultResource);
        var keyVaultId = keyVault.RootElement.GetProperty("id").GetGuid();
        var keyVaultVersion = keyVault.RootElement.GetProperty("version").GetInt32();

        using var setIrreversible = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{keyVaultId:D}/overrides",
            new
            {
                targetId = environment.Id,
                property = "purgeProtection",
                value = true,
                expectedVersion = keyVaultVersion
            });
        setIrreversible.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var setResponse = await client.SendAsync(setIrreversible);
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        using var setResult = await JsonDocument.ParseAsync(await setResponse.Content.ReadAsStreamAsync());
        var overriddenVersion = setResult.RootElement.GetProperty("version").GetInt32();

        using var clear = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{keyVaultId:D}/overrides",
            new
            {
                targetId = environment.Id,
                property = "purgeProtection",
                expectedVersion = overriddenVersion
            });
        clear.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var clearResponse = await client.SendAsync(clear);
        Assert.Equal(HttpStatusCode.BadRequest, clearResponse.StatusCode);
        Assert.Equal("RESOURCE_PROPERTY_IRREVERSIBLE", await GetProblemCodeAsync(clearResponse));

        using var reverse = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources/{keyVaultId:D}/overrides",
            new
            {
                targetId = environment.Id,
                property = "purgeProtection",
                value = false,
                expectedVersion = overriddenVersion
            });
        reverse.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var reverseResponse = await client.SendAsync(reverse);

        Assert.Equal(HttpStatusCode.BadRequest, reverseResponse.StatusCode);
        Assert.Equal("RESOURCE_PROPERTY_IRREVERSIBLE", await GetProblemCodeAsync(reverseResponse));
    }

    private async Task ComponentAndResourceGroupCanBeCreatedAndListedCore()
    {
        var expectedVersion = await PrepareAsync();
        var apiProjectVersion = await GetProjectVersionFromApiAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var componentCode = $"core-{suffix}";
        var initialComponentCount = await CountComponentsAsync();
        using var createComponent = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components",
            new
            {
                name = $"Core {suffix}",
                code = componentCode,
                description = "Infrastructure commune",
                mode = "PerEnvironment",
                targetsAllEnvironments = true,
                targetEnvironmentIds = Array.Empty<Guid>(),
                singleTarget = (object?)null,
                removedResourceBehavior = "Detach",
                deletionProtectionEnabled = true,
                tags = new Dictionary<string, string>(),
                expectedVersion
            });
        createComponent.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var componentResponse = await client.SendAsync(createComponent);

        Assert.True(
            componentResponse.StatusCode == HttpStatusCode.Created,
            $"expected project version {expectedVersion}, API project version {apiProjectVersion}: {await componentResponse.Content.ReadAsStringAsync()}");
        using var createdComponent = await JsonDocument.ParseAsync(await componentResponse.Content.ReadAsStreamAsync());
        var componentId = createdComponent.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(componentCode, createdComponent.RootElement.GetProperty("code").GetString());
        Assert.True(createdComponent.RootElement.GetProperty("targetsAllEnvironments").GetBoolean());
        Assert.Equal("Detach", createdComponent.RootElement.GetProperty("removedResourceBehavior").GetString());

        var createdComponentVersion = createdComponent.RootElement.GetProperty("version").GetInt32();
        using var getComponent = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}/components/{componentId:D}");
        using var getComponentResponse = await client.SendAsync(getComponent);
        Assert.Equal(HttpStatusCode.OK, getComponentResponse.StatusCode);
        using var currentComponent = await JsonDocument.ParseAsync(await getComponentResponse.Content.ReadAsStreamAsync());
        var componentVersion = currentComponent.RootElement.GetProperty("version").GetInt32();
        using var createGroup = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resource-groups",
            new
            {
                name = "main",
                region = (string?)null,
                tags = new Dictionary<string, string>(),
                forcedNames = new Dictionary<string, string>(),
                expectedVersion = componentVersion
            });
        createGroup.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var groupResponse = await client.SendAsync(createGroup);
        Assert.True(
            groupResponse.StatusCode == HttpStatusCode.Created,
            $"created version {createdComponentVersion}, current version {componentVersion}: {await groupResponse.Content.ReadAsStringAsync()}");
        using var createdGroup = await JsonDocument.ParseAsync(await groupResponse.Content.ReadAsStreamAsync());
        Assert.Equal("main", createdGroup.RootElement.GetProperty("name").GetString());
        Assert.Null(createdGroup.RootElement.GetProperty("region").GetString());

        using var listRequest = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}/components");
        using var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listed = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        Assert.Contains(listed.RootElement.EnumerateArray(), component => component.GetProperty("id").GetGuid() == componentId);

        using var projectRequest = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}");
        using var projectResponse = await client.SendAsync(projectRequest);
        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        using var project = await JsonDocument.ParseAsync(await projectResponse.Content.ReadAsStreamAsync());
        Assert.Equal(initialComponentCount + 1, project.RootElement.GetProperty("componentCount").GetInt32());
    }

    [Fact]
    public Task SingleComponentCannotReuseAnEnvironmentCodeAsItsOwnTarget() =>
        RunSerialAsync(SingleComponentCannotReuseAnEnvironmentCodeAsItsOwnTargetCore);

    private async Task SingleComponentCannotReuseAnEnvironmentCodeAsItsOwnTargetCore()
    {
        var expectedVersion = await PrepareAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components",
            new
            {
                name = $"Platform {suffix}",
                code = $"plat-{suffix}",
                description = "Shared services",
                mode = "Single",
                targetsAllEnvironments = false,
                targetEnvironmentIds = Array.Empty<Guid>(),
                singleTarget = new
                {
                    code = "dev",
                    subscriptionId = Guid.NewGuid(),
                    region = "francecentral",
                    deploymentConnection = (string?)null,
                    isProtected = true,
                    approvers = AliceApprovers,
                    runners = Array.Empty<string>()
                },
                removedResourceBehavior = "Detach",
                deletionProtectionEnabled = true,
                tags = new Dictionary<string, string>(),
                expectedVersion
            });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public Task SingleTargetCodeCannotBeAssignedToAProjectEnvironment() =>
        RunSerialAsync(SingleTargetCodeCannotBeAssignedToAProjectEnvironmentCore);

    private async Task SingleTargetCodeCannotBeAssignedToAProjectEnvironmentCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var expectedVersion = await GetProjectVersionFromApiAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var targetCode = $"s{suffix[..5]}";
        using var componentResponse = await SendCreateComponentAsync(new
        {
            name = $"Platform {suffix}",
            code = $"plat-{suffix}",
            description = "Shared services",
            mode = "Single",
            targetsAllEnvironments = false,
            targetEnvironmentIds = Array.Empty<Guid>(),
            singleTarget = new
            {
                code = targetCode,
                subscriptionId = Guid.NewGuid(),
                region = "francecentral",
                deploymentConnection = (string?)null,
                isProtected = true,
                approvers = AliceApprovers,
                runners = Array.Empty<string>()
            },
            removedResourceBehavior = "Detach",
            deletionProtectionEnabled = true,
            tags = new Dictionary<string, string>(),
            expectedVersion
        });
        Assert.Equal(HttpStatusCode.Created, componentResponse.StatusCode);
        var environmentVersion = await GetProjectVersionFromApiAsync();

        using var updateEnvironment = CreateRequest(
            HttpMethod.Put,
            $"/v1/projects/{ProjectId:D}/environments/{environment.Id:D}",
            new UpdateProjectEnvironmentRequest(
                environment.Name,
                targetCode,
                environment.SubscriptionId,
                environment.Region,
                environment.IsProtected,
                environment.Approvers,
                environment.Runners,
                environment.Description,
                environment.Tags,
                true,
                environmentVersion,
                environment.DeploymentConnection));
        updateEnvironment.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(updateEnvironment);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT",
            await GetProblemCodeAsync(response));
    }

    [Fact]
    public Task EnvironmentCannotBeDeletedAsTheOnlyComponentTarget() =>
        RunSerialAsync(EnvironmentCannotBeDeletedAsTheOnlyComponentTargetCore);

    private async Task EnvironmentCannotBeDeletedAsTheOnlyComponentTargetCore()
    {
        await PrepareAsync();
        var environment = await GetEditableEnvironmentAsync();
        var expectedVersion = await GetProjectVersionFromApiAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var componentResponse = await SendCreateComponentAsync(new
        {
            name = $"API {suffix}",
            code = $"api-{suffix}",
            description = "Environment-targeted component",
            mode = "PerEnvironment",
            targetsAllEnvironments = false,
            targetEnvironmentIds = new[] { environment.Id },
            singleTarget = (object?)null,
            removedResourceBehavior = "Detach",
            deletionProtectionEnabled = true,
            tags = new Dictionary<string, string>(),
            expectedVersion
        });
        Assert.Equal(HttpStatusCode.Created, componentResponse.StatusCode);
        var environmentVersion = await GetProjectVersionFromApiAsync();

        using var deleteEnvironment = CreateRequest(
            HttpMethod.Delete,
            $"/v1/projects/{ProjectId:D}/environments/{environment.Id:D}",
            new DeleteProjectEnvironmentRequest(true, environmentVersion));
        deleteEnvironment.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(deleteEnvironment);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("COMPONENT_TARGET_REQUIRED", await GetProblemCodeAsync(response));
    }

    private async Task<ProjectEnvironmentResponse> GetEditableEnvironmentAsync()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/v1/projects/{ProjectId:D}/environments");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var environments = await response.Content.ReadFromJsonAsync<ProjectEnvironmentResponse[]>();
        Assert.NotNull(environments);
        return Assert.IsType<ProjectEnvironmentResponse>(environments.First(environment => !environment.IsProtected));
    }

    private async Task<Guid> CreateResourceTestComponentAsync(ProjectEnvironmentResponse environment)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var response = await SendCreateComponentAsync(new
        {
            name = $"Resources {suffix}",
            code = $"res-{suffix}",
            description = "Resource integration test",
            mode = "PerEnvironment",
            targetsAllEnvironments = false,
            targetEnvironmentIds = new[] { environment.Id },
            singleTarget = (object?)null,
            removedResourceBehavior = "Detach",
            deletionProtectionEnabled = true,
            tags = new Dictionary<string, string>(),
            expectedVersion = await GetProjectVersionFromApiAsync()
        });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var component = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return component.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateResourceTestGroupAsync(Guid componentId)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resource-groups",
            new
            {
                name = "main",
                region = (string?)null,
                tags = new Dictionary<string, string>(),
                forcedNames = new Dictionary<Guid, string>(),
                expectedVersion = await GetComponentVersionFromApiAsync(componentId)
            });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var group = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return group.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> CreateResourceAsync(
        Guid componentId,
        Guid resourceGroupId,
        string type,
        string logicalName,
        IReadOnlyDictionary<string, object?> properties,
        bool isExisting = false,
        Guid? defaultIdentityResourceId = null)
    {
        var expectedVersion = await GetComponentVersionFromApiAsync(componentId);
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components/{componentId:D}/resources",
            new
            {
                resourceGroupId,
                type,
                logicalName,
                description = "Resource value persistence test",
                isExisting,
                defaultIdentityResourceId,
                properties,
                tags = new Dictionary<string, string>(),
                expectedVersion
            });
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected component version {expectedVersion}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<int> GetComponentVersionFromApiAsync(Guid componentId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}/components/{componentId:D}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var component = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return component.RootElement.GetProperty("version").GetInt32();
    }

    private Task<HttpResponseMessage> SendCreateComponentAsync(object body)
    {
        var request = CreateRequest(
            HttpMethod.Post,
            $"/v1/projects/{ProjectId:D}/components",
            body);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }

    private static async Task<string?> GetProblemCodeAsync(HttpResponseMessage response)
    {
        using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }

    private async Task<int> PrepareAsync()
    {
        using var scope = factory.Services.CreateScope();
        var currentOrganization = scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>();
        currentOrganization.Set(new OrganizationId(OrganizationId));
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);
        var project = await dbContext.Projects
            .Include(candidate => candidate.Environments)
            .SingleAsync(candidate => candidate.Id == DemoDataSeeder.ShopProjectId);
        if (project.Environments.Count == 0)
        {
            Assert.False(project.AddEnvironment(
                Guid.NewGuid(), "Development", "dev", Guid.NewGuid(), "francecentral", 1,
                false, [], [], "").IsError);
            Assert.False(project.AddEnvironment(
                Guid.NewGuid(), "Production", "prd", Guid.NewGuid(), "francecentral", 2,
                true, AliceApprovers, [], "").IsError);
            await dbContext.SaveChangesAsync();
        }

        return project.Version;
    }

    private async Task<int> CountComponentsAsync()
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HttpCurrentOrganization>().Set(new OrganizationId(OrganizationId));
        return await scope.ServiceProvider.GetRequiredService<IfsDbContext>()
            .Set<ComponentRoot>()
            .CountAsync(component => component.ProjectId == new ProjectId(ProjectId));
    }

    private async Task<int> GetProjectVersionFromApiAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v1/projects/{ProjectId:D}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var project = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return project.RootElement.GetProperty("version").GetInt32();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AliceToken);
        request.Headers.Add("X-Ifs-Organization", OrganizationId.ToString("D"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task RunSerialAsync(Func<Task> action)
    {
        await TestLock.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            TestLock.Release();
        }
    }
}
