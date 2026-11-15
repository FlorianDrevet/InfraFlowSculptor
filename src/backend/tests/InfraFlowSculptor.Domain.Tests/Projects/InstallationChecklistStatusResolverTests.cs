using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class InstallationChecklistStatusResolverTests
{
    [Fact]
    public void VerificationIsFreshAtThirtyDaysAndNeedsReviewAfterThat()
    {
        var verifiedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var atBoundary = verifiedAt.Add(InstallationChecklistStatusResolver.VerificationFreshness);
        var afterBoundary = atBoundary.AddTicks(1);

        Assert.Equal(
            InstallationChecklistItemState.Verified,
            InstallationChecklistStatusResolver.Resolve(InstallationChecklistItemState.ToDo, false, verifiedAt, atBoundary));
        Assert.Equal(
            InstallationChecklistItemState.NeedsReverification,
            InstallationChecklistStatusResolver.Resolve(InstallationChecklistItemState.ToDo, false, verifiedAt, afterBoundary));
    }
}
