using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;

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

        return v1;
    }
}
