using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class WorkerOrganizationContext : ICurrentOrganization
{
    public OrganizationId? Id { get; private set; }

    public void Set(OrganizationId organizationId) => Id = organizationId;
}
