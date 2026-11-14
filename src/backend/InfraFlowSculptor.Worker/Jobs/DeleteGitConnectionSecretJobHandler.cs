using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.GitConnections;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class DeleteGitConnectionSecretJobHandler(ISecretStore secretStore)
    : JobHandler<DeleteGitConnectionSecretJob>
{
    public override Task HandleAsync(
        DeleteGitConnectionSecretJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        secretStore.DeleteAsync(job.SecretName, cancellationToken);
}
