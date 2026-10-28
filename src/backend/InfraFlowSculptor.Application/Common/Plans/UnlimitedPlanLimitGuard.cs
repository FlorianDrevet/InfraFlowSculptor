using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Plans;

public sealed class UnlimitedPlanLimitGuard : IPlanLimitGuard
{
    public ErrorOr<Success> EnsureCanCreate(OrganizationId? organizationId, LimitKind kind) => Result.Success;
}
