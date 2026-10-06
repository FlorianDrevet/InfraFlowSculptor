using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IJobProcessingStatusStore
{
    Task<int> GetProcessedCountAsync(
        Guid jobId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default);
}
