using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Publications;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class PreparePublicationJobHandler(PublicationOperationProcessor processor) : JobHandler<PreparePublicationJob>
{
    public override Task HandleAsync(
        PreparePublicationJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        processor.PrepareAsync(job, context, cancellationToken);
}
