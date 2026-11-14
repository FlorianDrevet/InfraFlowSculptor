using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.GitConnections;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class CheckGitConnectionExpiryAlertsJobHandler(
    GitConnectionExpiryAlertProcessor processor) : JobHandler<CheckGitConnectionExpiryAlertsJob>
{
    public override Task HandleAsync(
        CheckGitConnectionExpiryAlertsJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        processor.CheckOrganizationAsync(context.OrganizationId, cancellationToken);
}
