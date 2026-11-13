using InfraFlowSculptor.Application.Generation;

namespace InfraFlowSculptor.Application.Tests.Generation;

public sealed class RevisionPlanDiffFactoryTests
{
    [Fact]
    public void CreateReportsChangedPlanPathsWithoutIncludingValues()
    {
        const string previous = "{\"project\":{\"name\":\"Shop\",\"code\":\"shop\"},\"targets\":[{\"code\":\"dev\",\"region\":\"francecentral\"}]}";
        const string current = "{\"project\":{\"name\":\"Shop\",\"code\":\"shop\"},\"targets\":[{\"code\":\"dev\",\"region\":\"westeurope\"}],\"components\":[{\"code\":\"api\"}]}";

        var changes = RevisionPlanDiffFactory.Create(previous, current);

        Assert.Collection(
            changes,
            change => Assert.Equal(("PlanAdded", "/components", "Plan de déploiement modifié", "Deployment plan changed"),
                (change.Category, change.Subject, change.DescriptionFr, change.DescriptionEn)),
            change => Assert.Equal(("PlanChanged", "/targets/0/region", "Plan de déploiement modifié", "Deployment plan changed"),
                (change.Category, change.Subject, change.DescriptionFr, change.DescriptionEn)));
        Assert.DoesNotContain("westeurope", string.Join(" ", changes.Select(change => change.Subject + change.DescriptionEn)), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateReturnsNoChangesForCanonicalPlansThatMatch()
    {
        const string plan = "{\"project\":{\"code\":\"shop\"}}";

        Assert.Empty(RevisionPlanDiffFactory.Create(plan, plan));
    }
}
