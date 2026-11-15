using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Mediator;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class EnqueueGitConnectionExpiryAlertsCommandHandler(
    ICurrentOrganization currentOrganization,
    IJobDispatcher dispatcher) : ICommandHandler<EnqueueGitConnectionExpiryAlertsCommand, ErrorOr<Guid>>
{
    public ValueTask<ErrorOr<Guid>> Handle(
        EnqueueGitConnectionExpiryAlertsCommand command,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(command.OrganizationId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return ValueTask.FromResult<ErrorOr<Guid>>(
                Error.NotFound("NOT_FOUND", "The organization was not found."));
        }

        var jobId = dispatcher.Enqueue(
            JobQueue.Notifications,
            organizationId,
            new CheckGitConnectionExpiryAlertsJob());
        return ValueTask.FromResult<ErrorOr<Guid>>(jobId);
    }
}
