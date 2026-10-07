using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InfraFlowSculptor.Acceptance.Tests;

[Collection(JobsTestCollection.Name)]
public sealed class JobsTests(JobsTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    [SuppressMessage("Naming", "CA1707", Justification = "The S-08 plan names this acceptance test Ping_job_is_processed_once.")]
    public async Task Ping_job_is_processed_once()
    {
        var organizationId = Guid.NewGuid();
        using var request = CreateRequest(HttpMethod.Post, "/v1/dev/ping-job", organizationId);
        using var response = await fixture.Client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var exceptionStack = document.RootElement.TryGetProperty("exceptionStack", out var stack)
            ? string.Join(Environment.NewLine, stack.EnumerateArray().Select(frame => frame.GetString()))
            : string.Empty;
        Assert.True(
            response.StatusCode == HttpStatusCode.Accepted,
            $"Expected HTTP {(int)HttpStatusCode.Accepted}, received {(int)response.StatusCode}: {body}{Environment.NewLine}{exceptionStack}");
        var submission = await response.Content.ReadFromJsonAsync<JobSubmission>();
        Assert.NotNull(submission);

        var status = await WaitForProcessedAsync(submission.JobId, organizationId, TimeSpan.FromSeconds(45));
        Assert.Equal(1, status.ProcessedCount);
    }

    [Fact]
    [Trait("Category", "Acceptance")]
    [SuppressMessage("Naming", "CA1707", Justification = "The S-08 plan names this acceptance test Sessions_are_served_fairly_under_saturation.")]
    public async Task Sessions_are_served_fairly_under_saturation()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var organizationC = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var producers = new[]
        {
            ProduceUntilCancelledAsync(organizationA, cancellation.Token),
            ProduceUntilCancelledAsync(organizationB, cancellation.Token)
        };

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            using var request = CreateRequest(
                HttpMethod.Post,
                "/v1/dev/ping-job?delayMilliseconds=200",
                organizationC);
            using var response = await fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var submission = await response.Content.ReadFromJsonAsync<JobSubmission>();
            Assert.NotNull(submission);

            var stopwatch = Stopwatch.StartNew();
            await WaitForProcessedAsync(submission.JobId, organizationC, TimeSpan.FromSeconds(10));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3),
                $"Organization C waited {stopwatch.Elapsed.TotalMilliseconds:N0} ms while A and B saturated the queue.");
        }
        finally
        {
            await cancellation.CancelAsync();
            await Task.WhenAll(producers);
        }
    }

    private async Task ProduceUntilCancelledAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var request = CreateRequest(
                    HttpMethod.Post,
                    "/v1/dev/ping-job?delayMilliseconds=200",
                    organizationId);
                using var response = await fixture.Client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Assert.True(
                    response.IsSuccessStatusCode,
                    $"Job submission returned HTTP {(int)response.StatusCode}: {body}");
                var submission = await response.Content.ReadFromJsonAsync<JobSubmission>(cancellationToken);
                if (submission is not null)
                {
                    await WaitForProcessedAsync(
                        submission.JobId,
                        organizationId,
                        TimeSpan.FromSeconds(20),
                        cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<JobStatus> WaitForProcessedAsync(
        Guid jobId,
        Guid organizationId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (!deadline.IsCancellationRequested)
            {
                using var request = CreateRequest(
                    HttpMethod.Get,
                    $"/v1/dev/ping-job/{jobId:D}",
                    organizationId);
                using var response = await fixture.Client.SendAsync(request, deadline.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return (await response.Content.ReadFromJsonAsync<JobStatus>(deadline.Token))!;
                }

                if (response.StatusCode != HttpStatusCode.Accepted)
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

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, Guid organizationId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            fixture.CreateAccessToken(organizationId));
        return request;
    }

    private sealed record JobSubmission(Guid JobId);

    private sealed record JobStatus(Guid JobId, bool Processed, int ProcessedCount);
}
