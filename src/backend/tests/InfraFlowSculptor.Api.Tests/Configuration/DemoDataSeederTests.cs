using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfraFlowSculptor.Api.Tests;
using InfraFlowSculptor.Api.Tests.Common;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Contracts.Projects;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Tests.Configuration;

public sealed class DemoDataSeederTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ReferenceCatalogVersion = "2026.10-pilot.6";

    [Fact]
    public async Task DemoProjectPinsTheCatalogVersionUsedByReferenceOutput()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await dbContext.Database.MigrateAsync();

        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System);

        var project = await dbContext.Projects
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == DemoDataSeeder.ShopProjectId);

        Assert.Equal(ReferenceCatalogVersion, project.CatalogVersion);
    }

    [Fact]
    public async Task DemoProjectIsReadyForRevisionGeneration()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await dbContext.Database.MigrateAsync();
        await DemoDataSeeder.SeedAsync(dbContext, TimeProvider.System, seedReferenceModel: true);

        var seededProject = await dbContext.Projects
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Environments)
            .SingleAsync(candidate => candidate.Id == DemoDataSeeder.ShopProjectId);
        Assert.Equal(2, seededProject.Environments.Count);

        var paymentKey = await dbContext.AppSettings
            .IgnoreQueryFilters()
            .SingleAsync(setting => setting.ProjectId == DemoDataSeeder.ShopProjectId);
        Assert.Equal("Payments__ApiKey", paymentKey.Name);
        Assert.Equal(AppSettingSourceKind.KeyVaultSecret, paymentKey.SourceKind);
        Assert.Equal("MAIN_PAYMENTS_API_KEY", paymentKey.PipelineVariableName);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/projects/{DemoDataSeeder.ShopProjectId.Value:D}/validation");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateAccessToken());
        request.Headers.Add("X-Ifs-Organization", "33333333-3333-4333-8333-333333333333");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var validation = await response.Content.ReadFromJsonAsync<ProjectValidationResponse>();

        Assert.NotNull(validation);
        Assert.True(
            validation.ErrorCount == 0,
            string.Join(Environment.NewLine, validation.Findings
                .Where(finding => finding.Severity == "Error")
                .Select(finding => finding.Code + ": " + finding.MessageFr)));
        Assert.True(validation.Readiness.RevisionGenerable);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void DemoSeedingIsLimitedToNonProductionEnvironments(string environmentName, bool expected)
    {
        Assert.Equal(expected, DemoDataSeeder.IsAllowedEnvironment(environmentName));
    }
}
