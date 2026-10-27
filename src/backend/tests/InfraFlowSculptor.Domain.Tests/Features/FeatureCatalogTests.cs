using InfraFlowSculptor.Domain.Common.Features;

namespace InfraFlowSculptor.Domain.Tests.Features;

public sealed class FeatureCatalogTests
{
    [Fact]
    public void EveryFeatureKeyIsUniqueAndHasAStatus()
    {
        Assert.NotEmpty(FeatureCatalog.All);
        Assert.Equal(
            FeatureCatalog.All.Count,
            FeatureCatalog.All.Select(feature => feature.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(FeatureCatalog.All, feature => Assert.Contains(feature.Status, Enum.GetValues<FeatureStatus>()));
    }

    [Fact]
    public void OnlyFeaturesDeliveredByThisStepAreReleased()
    {
        Assert.Equal(FeatureStatus.Released, FeatureCatalog.Find(global::InfraFlowSculptor.Domain.Common.Features.Features.Organizations)?.Status);
        Assert.Equal(FeatureStatus.Released, FeatureCatalog.Find(global::InfraFlowSculptor.Domain.Common.Features.Features.UserPreferences)?.Status);
        Assert.Equal(FeatureStatus.NotReleased, FeatureCatalog.Find(global::InfraFlowSculptor.Domain.Common.Features.Features.TerraformEmitter)?.Status);
        Assert.Equal(FeatureStatus.NotReleased, FeatureCatalog.Find(global::InfraFlowSculptor.Domain.Common.Features.Features.Drafts)?.Status);
    }
}
