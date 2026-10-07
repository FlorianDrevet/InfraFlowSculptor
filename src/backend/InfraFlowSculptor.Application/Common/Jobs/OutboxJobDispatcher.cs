using System.Diagnostics;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public sealed class OutboxJobDispatcher(
    IIfsDbContext dbContext,
    TimeProvider clock) : IJobDispatcher
{
    public Guid Enqueue<TJob>(JobQueue queue, OrganizationId organizationId, TJob job)
        where TJob : IJob
    {
        ArgumentNullException.ThrowIfNull(job);

        var now = clock.GetUtcNow();
        var jobId = Guid.CreateVersion7(now);
        var jobType = typeof(TJob).FullName ?? typeof(TJob).Name;
        var envelope = new JobEnvelope(
            jobId,
            organizationId,
            jobType,
            JsonSerializer.SerializeToElement(job, JobSerialization.Options),
            Activity.Current?.Id);

        dbContext.AddOutboxMessage(new OutboxMessageData(
            jobId,
            organizationId,
            JobQueueNames.GetName(queue),
            organizationId.Value.ToString("D"),
            jobType,
            JsonSerializer.SerializeToElement(envelope, JobSerialization.Options),
            now));

        return jobId;
    }
}
