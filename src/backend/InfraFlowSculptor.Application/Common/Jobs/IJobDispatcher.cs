using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IJobDispatcher
{
    Guid Enqueue<TJob>(JobQueue queue, OrganizationId organizationId, TJob job)
        where TJob : IJob;
}
