using InfraFlowSculptor.Application.Common.Features;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Features;

public sealed class EfFeatureAvailability(IIfsDbContext dbContext) : IFeatureAvailability
{
    public Task<bool> IsEnabledAsync(
        string featureKey,
        OrganizationId? organizationId,
        CancellationToken cancellationToken)
    {
        var feature = FeatureCatalog.Find(featureKey);
        if (feature is null || feature.Status == FeatureStatus.NotReleased)
        {
            return Task.FromResult(false);
        }

        if (feature.Status == FeatureStatus.Released)
        {
            return Task.FromResult(true);
        }

        if (organizationId is not { } organization)
        {
            return Task.FromResult(false);
        }

        return dbContext.Query<OrganizationFeatureAccessEntity>()
            .AnyAsync(
                access => access.OrganizationId == organization && access.FeatureKey == featureKey,
                cancellationToken);
    }
}
