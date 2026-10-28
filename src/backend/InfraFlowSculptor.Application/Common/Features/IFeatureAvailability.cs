using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Features;

public interface IFeatureAvailability
{
    Task<bool> IsEnabledAsync(
        string featureKey,
        OrganizationId? organizationId,
        CancellationToken cancellationToken);
}
