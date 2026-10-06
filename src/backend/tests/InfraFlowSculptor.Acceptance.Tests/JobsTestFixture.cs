using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.IdentityModel.Tokens;

namespace InfraFlowSculptor.Acceptance.Tests;

public sealed class JobsTestFixture : IAsyncLifetime
{
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await application.StartAsync(timeout.Token);
        foreach (var resource in new[]
        {
            "postgres",
            "storage",
            "servicebus",
            "redis"
        })
        {
            await application.ResourceNotifications.WaitForResourceHealthyAsync(resource, timeout.Token);
        }

        Client = application.CreateHttpClient("api");
        await WaitForApiAsync(timeout.Token);
        await WarmWorkerAsync(timeout.Token);
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var response = await Client.GetAsync("/alive", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }

    private sealed record JobSubmission(Guid JobId);

    private sealed record JobStatus(Guid JobId, bool Processed, int ProcessedCount);
}
