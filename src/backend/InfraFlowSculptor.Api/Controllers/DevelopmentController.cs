using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class DevelopmentController
{
    public static RouteGroupBuilder MapDevelopmentEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/dev/ping-job", async (
                int? delayMilliseconds,
                ICurrentOrganization currentOrganization,
                IJobDispatcher dispatcher,
                IIfsDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                if (currentOrganization.Id is not { } organizationId)
                {
                    return Results.Forbid();
                }

                await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
                var jobId = dispatcher.Enqueue(
                    JobQueue.Generation,
                    organizationId,
                    new PingJob(Math.Clamp(delayMilliseconds ?? 0, 0, 30_000)));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Results.Accepted($"/v1/dev/ping-job/{jobId:D}", new { jobId });
            })
            .RequireAuthorization()
            .ExcludeFromDescription()
            .WithName("EnqueueDevelopmentPingJob");

        v1.MapGet("/dev/ping-job/{jobId:guid}", async (
                Guid jobId,
                ICurrentOrganization currentOrganization,
                IJobProcessingStatusStore statusStore,
                CancellationToken cancellationToken) =>
            {
                if (currentOrganization.Id is not { } organizationId)
                {
                    return Results.Forbid();
                }

                var processedCount = await statusStore.GetProcessedCountAsync(
                    jobId,
                    organizationId,
                    cancellationToken);

                return processedCount > 0
                    ? Results.Ok(new { jobId, processed = true, processedCount })
                    : Results.Accepted($"/v1/dev/ping-job/{jobId:D}", new { jobId, processed = false });
            })
            .RequireAuthorization()
            .ExcludeFromDescription()
            .WithName("GetDevelopmentPingJobStatus");

        v1.MapPost("/dev/git-connections/expiry-alerts", async (
                ICurrentOrganization currentOrganization,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                if (currentOrganization.Id is not { } organizationId)
                {
                    return Results.Forbid();
                }

                var result = await mediator.Send(
                    new EnqueueGitConnectionExpiryAlertsCommand(organizationId.Value),
                    cancellationToken);
                return result.Match<IResult>(
                    CreateExpiryAlertAcceptedResult,
                    errors => errors.ToProblem());
            })
            .RequireAuthorization()
            .WithIdempotency()
            .ExcludeFromDescription()
            .WithName("EnqueueDevelopmentGitConnectionExpiryAlerts");

        v1.MapGet("/dev/git-connections/expiry-alerts/{jobId:guid}", async (
                Guid jobId,
                ICurrentOrganization currentOrganization,
                IJobProcessingStatusStore statusStore,
                CancellationToken cancellationToken) =>
            {
                if (currentOrganization.Id is not { } organizationId)
                {
                    return Results.Forbid();
                }

                var processedCount = await statusStore.GetProcessedCountAsync(jobId, organizationId, cancellationToken);
                return processedCount > 0
                    ? Results.Ok(new { jobId, completed = true })
                    : Results.Accepted($"/v1/dev/git-connections/expiry-alerts/{jobId:D}", new { jobId, completed = false });
            })
            .RequireAuthorization()
            .ExcludeFromDescription()
            .WithName("GetDevelopmentGitConnectionExpiryAlertsStatus");

        return v1;
    }

    internal static IResult CreateExpiryAlertAcceptedResult(Guid jobId) =>
        Results.Accepted($"/v1/dev/git-connections/expiry-alerts/{jobId:D}", new { jobId });
}
