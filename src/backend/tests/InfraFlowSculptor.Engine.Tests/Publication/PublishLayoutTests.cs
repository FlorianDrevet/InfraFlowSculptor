using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Publication;

namespace InfraFlowSculptor.Engine.Tests.Publication;

public sealed class PublishLayoutTests
{
    private static readonly Guid ConnectionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DestinationId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid InfrastructureComponentId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ApplicationComponentId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void BuildDistributesCompletePilotTreeToOneDestinationAndAppliesBasePath()
    {
        var plan = CreateMonoRepositoryPlan("release");
        var layout = PublishLayout.Build(
            plan,
            "AzureDevOps",
            [
                new PublishComponent(InfrastructureComponentId, "core", false),
                new PublishComponent(ApplicationComponentId, "orders", true)
            ],
            [
                ".ifs/manifest-template.json",
                "README.ifs.md",
                "release.shared.json",
                "core/infra/main.bicep",
                "orders/infra/pipelines/ci.yml",
                "orders/apps/api/Dockerfile"
            ],
            new Dictionary<Guid, GitProviderKind> { [DestinationId] = GitProviderKind.AzureRepos });

        Assert.Empty(layout.Issues);
        Assert.Equal(6, layout.Files.Count);
        Assert.All(layout.Files, file => Assert.Equal(DestinationId, file.DestinationId));
        Assert.Contains(layout.Files, file => file.SourcePath == ".ifs/manifest-template.json"
            && file.DestinationPath == "release/.ifs/manifest-template.json");
        Assert.Contains(layout.Files, file => file.SourcePath == "README.ifs.md"
            && file.DestinationPath == "release/README.ifs.md");
        Assert.Contains(layout.Files, file => file.SourcePath == "core/infra/main.bicep"
            && file.DestinationPath == "release/core/infra/main.bicep");
        Assert.Contains(layout.Files, file => file.SourcePath == "orders/apps/api/Dockerfile"
            && file.DestinationPath == "release/orders/apps/api/Dockerfile");
    }

    [Fact]
    public void EffectiveFolderCombinesBasePathWithGeneratedAndSourceFolders()
    {
        Assert.Equal("teams/shop/core/infra", PublishLayout.EffectiveFolder("core", PublishPart.Infrastructure, "teams/shop"));
        Assert.Equal("orders/apps", PublishLayout.EffectiveFolder("orders", PublishPart.Applications, string.Empty));
        Assert.Equal("src/", PublishLayout.EffectiveFolder("orders", PublishPart.SourceCode, string.Empty, "src"));
    }

    [Fact]
    public void BuildCopiesCommonFilesToEveryUsedDestination()
    {
        var plan = CreateInfraAndCodePlan();
        var layout = PublishLayout.Build(
            plan,
            "AzureDevOps",
            [new PublishComponent(InfrastructureComponentId, "core", true)],
            [".ifs/manifest-template.json", "README.ifs.md", "core/infra/main.bicep", "core/apps/api/Dockerfile"],
            new Dictionary<Guid, GitProviderKind>
            {
                [DestinationId] = GitProviderKind.AzureRepos,
                [Guid.Parse("55555555-5555-4555-8555-555555555555")] = GitProviderKind.AzureRepos
            });

        Assert.Empty(layout.Issues);
        Assert.Equal(6, layout.Files.Count);
        Assert.Equal(2, layout.Files.Count(file => file.SourcePath == ".ifs/manifest-template.json"));
        Assert.Equal(2, layout.Files.Count(file => file.SourcePath == "README.ifs.md"));
    }

    [Fact]
    public void BuildReportsIncompatibleCiAndProviderPair()
    {
        var plan = CreateMonoRepositoryPlan(string.Empty);
        var layout = PublishLayout.Build(
            plan,
            "AzureDevOps",
            [new PublishComponent(InfrastructureComponentId, "core", false)],
            ["core/infra/main.bicep"],
            new Dictionary<Guid, GitProviderKind> { [DestinationId] = GitProviderKind.Gitea });

        Assert.Contains(layout.Issues, issue => issue.Code == "VAL-PIP-COMPATIBILITE");
    }

    [Fact]
    public void BuildReportsMissingDestinationForPerComponentPreset()
    {
        var plan = PublishPlan.CreateDefault();
        var result = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.OneRepositoryPerComponent,
            [],
            [],
            null,
            null,
            null));
        Assert.False(result.IsError);

        var layout = PublishLayout.Build(
            plan,
            "AzureDevOps",
            [new PublishComponent(InfrastructureComponentId, "core", false)],
            ["core/infra/main.bicep"],
            new Dictionary<Guid, GitProviderKind>());

        Assert.Contains(layout.Issues, issue => issue.Code == "VAL-PUB-DESTINATION");
    }

    private static PublishPlan CreateMonoRepositoryPlan(string basePath)
    {
        var plan = PublishPlan.CreateDefault();
        var result = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.MonoRepo,
            [new PublishPlanDestinationInput(DestinationId, ConnectionId, "contoso/shop", basePath)],
            [],
            DestinationId,
            DestinationId,
            DestinationId));
        Assert.False(result.IsError);
        return plan;
    }

    private static PublishPlan CreateInfraAndCodePlan()
    {
        var codeDestinationId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var plan = PublishPlan.CreateDefault();
        var result = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.InfrastructureAndCodeSeparated,
            [
                new PublishPlanDestinationInput(DestinationId, ConnectionId, "contoso/shop-infra", string.Empty),
                new PublishPlanDestinationInput(codeDestinationId, Guid.Parse("66666666-6666-4666-8666-666666666666"), "contoso/shop-app", string.Empty)
            ],
            [],
            DestinationId,
            codeDestinationId,
            codeDestinationId));
        Assert.False(result.IsError);
        return plan;
    }
}
