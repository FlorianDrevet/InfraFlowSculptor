using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class NoProjectOwnershipCheck : IProjectOwnershipCheck
{
    public Task<bool> WouldRemovingMemberRemoveLastProjectOwnerAsync(
        OrganizationId organizationId,
        UserProfileId userProfileId,
        CancellationToken cancellationToken) => Task.FromResult(false);
}
