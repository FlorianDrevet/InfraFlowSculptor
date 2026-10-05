using System.Net;
using Aspire.Hosting.Testing;

namespace InfraFlowSculptor.Acceptance.Tests;

public sealed class AppHostStartupTests
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task ApiIsHealthyWhenAllEmulatorsRun()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.InfraFlowSculptor_AppHost>();
        appHost.Environment.EnvironmentName = "Development";
        appHost.Configuration["Parameters:postgres-password"] = "Ifs-Acceptance-Postgres-2026!";
        appHost.Configuration["Parameters:keycloak-admin-password"] = "Ifs-Acceptance-Keycloak-2026!";

        await using var application = await appHost.BuildAsync();
        await application.StartAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        foreach (var resource in new[] { "postgres", "storage", "servicebus", "redis", "mailpit", "keycloak" })
        {
            try
            {
                await application.ResourceNotifications.WaitForResourceHealthyAsync(resource, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                throw new TimeoutException($"Resource '{resource}' did not become healthy before the acceptance timeout.", exception);
            }
        }

        using var client = application.CreateHttpClient("api");
        var readinessDeadline = DateTimeOffset.UtcNow.AddMinutes(3);
        HttpStatusCode? lastStatusCode = null;
        while (DateTimeOffset.UtcNow < readinessDeadline)
        {
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var response = await client.GetAsync("/health", requestTimeout.Token);
                lastStatusCode = response.StatusCode;
                if (lastStatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (OperationCanceledException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.Fail($"GET /health did not return 200 before the acceptance timeout. Last status: {lastStatusCode?.ToString() ?? "no response"}.");
    }
}
