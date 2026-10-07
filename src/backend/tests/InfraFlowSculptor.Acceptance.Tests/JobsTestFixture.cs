using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using InfraFlowSculptor.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InfraFlowSculptor.Acceptance.Tests;

public sealed class JobsTestFixture : IAsyncLifetime
{
    private static readonly TimeSpan ResourceHealthTimeout = TimeSpan.FromMinutes(5);
    private readonly string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string postgresPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private DistributedApplication? application;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.InfraFlowSculptor_AppHost>(
            args:
            [
                "--AppHost:PersistentContainers=false",
                $"--Auth:TestSigningKey={signingKey}",
                $"--Parameters:postgres-password={postgresPassword}"
            ],
            configureBuilder: (_, settings) => settings.EnvironmentName = "Testing");

        application = await appHost.BuildAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(25));
            await application.StartAsync(timeout.Token);
            foreach (var resource in new[]
            {
                ResourceNames.Postgres,
                ResourceNames.Storage,
                ResourceNames.ServiceBus,
                ResourceNames.Redis
            })
            {
                await WaitForResourceHealthyAsync(resource, timeout.Token);
            }

            // Keep acceptance probes independent of trusting a local development certificate in Linux CI.
            Client = application.CreateHttpClient(ResourceNames.Api, "http");
            await WaitForApiAsync(timeout.Token);
            await WarmWorkerAsync(timeout.Token);
        }
        catch (Exception initializationException)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "The acceptance AppHost failed to initialize and could not be cleaned up.",
                    initializationException,
                    cleanupException);
            }

            throw;
        }
    }

    private async Task WarmWorkerAsync(CancellationToken cancellationToken)
    {
        var organizationId = Guid.NewGuid();
        using var request = CreateRequest(HttpMethod.Post, "/v1/dev/ping-job", organizationId);
        using var response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var submission = await response.Content.ReadFromJsonAsync<JobSubmission>(cancellationToken);
        if (submission is null)
        {
            throw new InvalidOperationException("The worker warmup did not return a job ID.");
        }

        await WaitForProcessedAsync(submission.JobId, organizationId, TimeSpan.FromSeconds(45), cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, Guid organizationId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            CreateAccessToken(organizationId));
        return request;
    }

    private async Task WaitForProcessedAsync(
        Guid jobId,
        Guid organizationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (!deadline.IsCancellationRequested)
            {
                using var request = CreateRequest(HttpMethod.Get, $"/v1/dev/ping-job/{jobId:D}", organizationId);
                using var response = await Client.SendAsync(request, deadline.Token);
                if (response.IsSuccessStatusCode)
                {
                    var status = await response.Content.ReadFromJsonAsync<JobStatus>(deadline.Token);
                    if (status?.Processed == true && status.ProcessedCount == 1)
                    {
                        return;
                    }
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    response.EnsureSuccessStatusCode();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Ping job {jobId} was not processed within {timeout}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException($"Ping job {jobId} was not processed within {timeout}.");
    }

    private async Task WaitForApiAsync(CancellationToken cancellationToken)
    {
        await WaitForResourceHealthyAsync(ResourceNames.Api, cancellationToken);
        using var response = await Client.GetAsync("/alive", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task WaitForResourceHealthyAsync(string resourceName, CancellationToken cancellationToken)
    {
        using var resourceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        resourceTimeout.CancelAfter(ResourceHealthTimeout);

        try
        {
            await application!.ResourceNotifications.WaitForResourceHealthyAsync(
                resourceName,
                WaitBehavior.StopOnResourceUnavailable,
                resourceTimeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Aspire resource '{resourceName}' did not become healthy within {ResourceHealthTimeout}. " +
                GetResourceDiagnostics(resourceName),
                exception);
        }
        catch (DistributedApplicationException exception)
        {
            throw new InvalidOperationException(
                $"Aspire resource '{resourceName}' became unavailable. {GetResourceDiagnostics(resourceName)}",
                exception);
        }
    }

    private string GetResourceDiagnostics(string resourceName)
    {
        var currentApplication = application;
        if (currentApplication is null)
        {
            return $"No current Aspire state is available for resource '{resourceName}' because the AppHost is unavailable.";
        }

        var model = currentApplication.Services.GetRequiredService<DistributedApplicationModel>();
        var resourceStates = model.Resources
            .OrderBy(resource => resource.Name, StringComparer.Ordinal)
            .Select(resource =>
            {
                if (!currentApplication.ResourceNotifications.TryGetCurrentState(resource.Name, out var resourceEvent))
                {
                    return $"{resource.Name}: no current state";
                }

                var snapshot = resourceEvent.Snapshot;
                var state = snapshot.State?.Text ?? "unknown";
                var health = snapshot.HealthStatus?.ToString() ?? "unknown";
                var checks = string.Join(", ", snapshot.HealthReports.Select(report => $"{report.Name}={report.Status}"));
                if (checks.Length == 0)
                {
                    checks = "none";
                }

                var created = snapshot.CreationTimeStamp?.ToString("O") ?? "null";
                var started = snapshot.StartTimeStamp?.ToString("O") ?? "null";
                var stopped = snapshot.StopTimeStamp?.ToString("O") ?? "null";
                var exitCode = snapshot.ExitCode?.ToString() ?? "null";

                return $"{resource.Name}: state={state}; health={health}; created={created}; started={started}; stopped={stopped}; exitCode={exitCode}; checks={checks}";
            });

        return $"Current Aspire resource states while waiting for '{resourceName}':{Environment.NewLine}" +
            string.Join(Environment.NewLine, resourceStates.Select(state => $"- {state}"));
    }

    public string CreateAccessToken(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            new Claim("tid", tenantId.ToString("D")),
            new Claim("oid", Guid.NewGuid().ToString("D")),
            new Claim("name", "Acceptance User")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            audience: "ifs-api",
            claims: claims,
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.AddMinutes(30).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (application is not null)
        {
            var currentApplication = application;
            application = null;
            try
            {
                await currentApplication.StopAsync();
            }
            finally
            {
                await currentApplication.DisposeAsync();
            }
        }
    }

    private sealed record JobSubmission(Guid JobId);

    private sealed record JobStatus(Guid JobId, bool Processed, int ProcessedCount);
}
