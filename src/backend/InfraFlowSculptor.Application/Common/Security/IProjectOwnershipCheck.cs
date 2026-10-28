using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Security;

public interface IProjectOwnershipCheck
{
    Task<bool> WouldRemovingMemberRemoveLastProjectOwnerAsync(
        OrganizationId organizationId,
        UserProfileId userProfileId,
        CancellationToken cancellationToken);
}
