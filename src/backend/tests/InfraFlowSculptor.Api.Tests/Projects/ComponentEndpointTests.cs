using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Contracts.Projects;
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
        bool isExisting = false)
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
