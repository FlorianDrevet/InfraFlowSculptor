using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class OrganizationFeatureAccessEntity : IOrganizationOwned
{
    public OrganizationId OrganizationId { get; private set; }

    public string FeatureKey { get; private set; } = string.Empty;

    private OrganizationFeatureAccessEntity()
    {
    }

    public OrganizationFeatureAccessEntity(OrganizationId organizationId, string featureKey)
    {
        OrganizationId = organizationId;
        FeatureKey = featureKey;
    }
}
