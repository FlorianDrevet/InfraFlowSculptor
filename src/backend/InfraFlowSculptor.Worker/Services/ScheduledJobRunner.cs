using System.Diagnostics;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Worker.Jobs;

namespace InfraFlowSculptor.Worker.Services;

public sealed partial class ScheduledJobRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ScheduledJobRunner> logger) : BackgroundService
{
    private readonly string holder = $"{Environment.MachineName}/{Guid.NewGuid():N}";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LeaseRenewInterval = TimeSpan.FromSeconds(20);
    private readonly Dictionary<string, int> consecutiveFailures = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextPoll = await RunDueJobsAsync(stoppingToken);
                await Task.Delay(nextPoll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                RunnerFailure(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<TimeSpan> RunDueJobsAsync(CancellationToken cancellationToken)
    {
        var nextPoll = PollInterval;
        await using var scope = scopeFactory.CreateAsyncScope();
        var leaseStore = scope.ServiceProvider.GetRequiredService<IScheduledJobLeaseStore>();
        foreach (var job in scope.ServiceProvider.GetServices<IScheduledJob>())
        {
            var now = clock.GetUtcNow();
            if (!await leaseStore.TryAcquireAsync(job.Name, holder, now, now.Add(LeaseDuration), cancellationToken))
            {
                continue;
            }

            using var activity = JobActivity.Source.StartActivity(
                "scheduled-job.run",
                ActivityKind.Internal);
            activity?.SetTag("job.name", job.Name);

            using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var leaseLost = RenewLeaseWhileRunningAsync(job.Name, jobCancellation);
            var succeeded = false;
            TimeSpan? failureBackoff = null;
            try
            {
                await job.RunAsync(jobCancellation.Token);
                succeeded = true;
                consecutiveFailures.Remove(job.Name);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                JobFailure(logger, exception, job.Name);
                consecutiveFailures.TryGetValue(job.Name, out var priorFailures);
                consecutiveFailures[job.Name] = priorFailures + 1;
                failureBackoff = GetFailureBackoff(job.Name);
            }
            catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException exception) when (!jobCancellation.IsCancellationRequested)
            {
                JobFailure(logger, exception, job.Name);
                consecutiveFailures.TryGetValue(job.Name, out var priorFailures);
                consecutiveFailures[job.Name] = priorFailures + 1;
                failureBackoff = GetFailureBackoff(job.Name);
            }
            finally
            {
                jobCancellation.Cancel();
                var ownershipWasLost = await leaseLost;
                var completedAt = clock.GetUtcNow();
                var nextEligibleAt = cancellationToken.IsCancellationRequested || ownershipWasLost
                    ? completedAt
                    : succeeded
                        ? completedAt.Add(job.Interval)
                        : completedAt.Add(failureBackoff ?? GetFailureBackoff(job.Name));
                await leaseStore.ReleaseAsync(job.Name, holder, nextEligibleAt, CancellationToken.None);
            }

            if (failureBackoff is { } delay && delay < nextPoll)
            {
                nextPoll = delay;
            }
        }

        return nextPoll;
    }

    private async Task<bool> RenewLeaseWhileRunningAsync(string jobName, CancellationTokenSource jobCancellation)
    {
        while (!jobCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseRenewInterval, jobCancellation.Token);
                await using var scope = scopeFactory.CreateAsyncScope();
                var leaseStore = scope.ServiceProvider.GetRequiredService<IScheduledJobLeaseStore>();
                var renewed = await leaseStore.RenewAsync(
                    jobName,
                    holder,
                    clock.GetUtcNow().Add(LeaseDuration),
                    jobCancellation.Token);
                if (!renewed)
                {
                    LeaseLost(logger, jobName);
                    jobCancellation.Cancel();
                    return true;
                }
            }
            catch (OperationCanceledException) when (jobCancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception exception)
            {
                LeaseRenewalFailure(logger, exception, jobName);
                jobCancellation.Cancel();
                return true;
            }
        }

        return false;
    }

    private TimeSpan GetFailureBackoff(string jobName)
    {
        consecutiveFailures.TryGetValue(jobName, out var failures);
        var seconds = Math.Min(1 << Math.Min(Math.Max(failures, 1), 8), 300);
        return TimeSpan.FromSeconds(seconds);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled job runner failed; it will retry shortly.")]
    private static partial void RunnerFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled job {JobName} failed.")]
    private static partial void JobFailure(ILogger logger, Exception exception, string jobName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled job {JobName} lost its lease; execution will be cancelled.")]
    private static partial void LeaseLost(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not renew the lease for scheduled job {JobName}; execution will be cancelled.")]
    private static partial void LeaseRenewalFailure(ILogger logger, Exception exception, string jobName);
}
