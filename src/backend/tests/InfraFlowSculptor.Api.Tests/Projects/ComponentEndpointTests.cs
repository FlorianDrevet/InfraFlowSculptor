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
