using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IOrganizationJobBudgetStore
{
    Task<bool> TryConsumeAsync(
        OrganizationId organizationId,
        int defaultJobsPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
