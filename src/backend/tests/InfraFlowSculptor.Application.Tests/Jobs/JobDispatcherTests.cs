using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using NSubstitute;
using System.Text.Json;

namespace InfraFlowSculptor.Application.Tests.Jobs;

public sealed class JobDispatcherTests
{
    [Fact]
    public void EnqueueWritesAnEnvelopeToTheOutboxWithoutSendingItImmediately()
    {
        OutboxMessageData? captured = null;
        var dbContext = Substitute.For<IIfsDbContext>();
        dbContext.AddOutboxMessage(Arg.Do<OutboxMessageData>(message => captured = message));
        var organizationId = new OrganizationId(Guid.NewGuid());
        var dispatcher = new OutboxJobDispatcher(dbContext, TimeProvider.System);

        var jobId = dispatcher.Enqueue(JobQueue.Generation, organizationId, new PingJob(200));

        Assert.NotNull(captured);
        Assert.Equal(jobId, captured.Id);
        Assert.Equal(organizationId, captured.OrganizationId);
        Assert.Equal(JobQueueNames.Generation, captured.Queue);
        Assert.Equal(organizationId.Value.ToString("D"), captured.SessionId);
        Assert.Equal(typeof(PingJob).FullName, captured.Type);
        var envelope = captured.Payload.Deserialize<JobEnvelope>(JobSerialization.Options);
        Assert.NotNull(envelope);
        Assert.Equal(jobId, envelope.JobId);
        Assert.Equal(organizationId, envelope.OrganizationId);
        Assert.Equal(typeof(PingJob).FullName, envelope.Type);
        Assert.Equal(200, envelope.Payload.GetProperty("delayMilliseconds").GetInt32());
    }
}
