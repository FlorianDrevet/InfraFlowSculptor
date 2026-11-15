using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Publications;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class WritePublicationJobHandler(PublicationOperationProcessor processor) : JobHandler<WritePublicationJob>
{
    public override Task HandleAsync(
        WritePublicationJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        processor.WriteAsync(job, context, cancellationToken);
}
