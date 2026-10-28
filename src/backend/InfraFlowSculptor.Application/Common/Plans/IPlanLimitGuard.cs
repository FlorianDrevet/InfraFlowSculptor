using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Plans;

public interface IPlanLimitGuard
{
    ErrorOr<Success> EnsureCanCreate(OrganizationId? organizationId, LimitKind kind);
}
