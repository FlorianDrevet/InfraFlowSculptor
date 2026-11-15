using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class PublishPlanTests
{
    private static readonly Guid ConnectionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid InfrastructureDestinationId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid CodeDestinationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ComponentId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void DestinationNormalizesRelativeBasePath()
    {
        var destination = PublishPlanDestination.Create(new PublishPlanDestinationInput(
            InfrastructureDestinationId,
            ConnectionId,
            "contoso/shop",
            "teams//shop-infra/"));

        Assert.False(destination.IsError);
        Assert.Equal("teams/shop-infra", destination.Value.BasePath);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("teams/../outside")]
    [InlineData("teams\\..\\outside")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    public void DestinationRejectsRootedAndParentBasePaths(string basePath)
    {
        var destination = PublishPlanDestination.Create(new PublishPlanDestinationInput(
            InfrastructureDestinationId,
            ConnectionId,
            "contoso/shop",
            basePath));

        Assert.True(destination.IsError);
        Assert.Equal("VAL-PUB-DESTINATION", Assert.Single(destination.Errors).Code);
    }

    [Fact]
    public void DestinationRejectsBasePathLongerThanTwoHundredCharacters()
    {
        var destination = PublishPlanDestination.Create(new PublishPlanDestinationInput(
            InfrastructureDestinationId,
            ConnectionId,
            "contoso/shop",
            new string('a', 201)));

        Assert.True(destination.IsError);
        Assert.Equal("VAL-PUB-DESTINATION", Assert.Single(destination.Errors).Code);
    }

    [Fact]
    public void MonoRepoDefaultAppliesToNewComponentsAndAllParts()
    {
        var plan = PublishPlan.CreateDefault();
        var configured = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.MonoRepo,
            [new PublishPlanDestinationInput(InfrastructureDestinationId, ConnectionId, "contoso/shop", string.Empty)],
            [],
            InfrastructureDestinationId,
            InfrastructureDestinationId,
            InfrastructureDestinationId));

        Assert.False(configured.IsError);
        Assert.Equal(InfrastructureDestinationId, plan.ResolveDestination(Guid.NewGuid(), PublishPart.Infrastructure));
        Assert.Equal(InfrastructureDestinationId, plan.ResolveDestination(Guid.NewGuid(), PublishPart.Applications));
        Assert.Equal(InfrastructureDestinationId, plan.ResolveDestination(Guid.NewGuid(), PublishPart.SourceCode));
    }

    [Fact]
    public void InfraAndCodePresetSeparatesInfrastructureFromApplicationsAndSourceCode()
    {
        var plan = PublishPlan.CreateDefault();
        var configured = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.InfrastructureAndCodeSeparated,
            [
                new PublishPlanDestinationInput(InfrastructureDestinationId, ConnectionId, "contoso/shop-infra", string.Empty),
                new PublishPlanDestinationInput(CodeDestinationId, ConnectionId, "contoso/shop-app", string.Empty)
            ],
            [],
            InfrastructureDestinationId,
            CodeDestinationId,
            CodeDestinationId));

        Assert.False(configured.IsError);
        Assert.Equal(InfrastructureDestinationId, plan.ResolveDestination(ComponentId, PublishPart.Infrastructure));
        Assert.Equal(CodeDestinationId, plan.ResolveDestination(ComponentId, PublishPart.Applications));
        Assert.Equal(CodeDestinationId, plan.ResolveDestination(ComponentId, PublishPart.SourceCode));
    }

    [Fact]
    public void PerComponentPresetLeavesNewComponentDestinationUnassigned()
    {
        var plan = PublishPlan.CreateDefault();
        var configured = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.OneRepositoryPerComponent,
            [new PublishPlanDestinationInput(InfrastructureDestinationId, ConnectionId, "contoso/shop", string.Empty)],
            [new PublishPlanAssignmentInput(ComponentId, PublishPart.Infrastructure, InfrastructureDestinationId)],
            null,
            null,
            null));

        Assert.False(configured.IsError);
        Assert.Equal(InfrastructureDestinationId, plan.ResolveDestination(ComponentId, PublishPart.Infrastructure));
        Assert.Null(plan.ResolveDestination(Guid.NewGuid(), PublishPart.Infrastructure));
    }

    [Fact]
    public void ConfigureRejectsNestedBasePathsInOneRepository()
    {
        var plan = PublishPlan.CreateDefault();
        var configured = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.InfrastructureAndCodeSeparated,
            [
                new PublishPlanDestinationInput(InfrastructureDestinationId, ConnectionId, "contoso/shop", string.Empty),
                new PublishPlanDestinationInput(CodeDestinationId, ConnectionId, "contoso/shop", "src")
            ],
            [],
            InfrastructureDestinationId,
            CodeDestinationId,
            CodeDestinationId));

        Assert.True(configured.IsError);
        Assert.Equal("VAL-PUB-DESTINATION", Assert.Single(configured.Errors).Code);
    }

    [Fact]
    public void ConfigureRejectsNestedBasePathsWhenRepositoryIdsDifferOnlyByCase()
    {
        var plan = PublishPlan.CreateDefault();
        var configured = plan.Configure(new PublishPlanConfiguration(
            PublishPreset.InfrastructureAndCodeSeparated,
            [
                new PublishPlanDestinationInput(InfrastructureDestinationId, ConnectionId, "contoso/shop", "services"),
                new PublishPlanDestinationInput(CodeDestinationId, ConnectionId, "CONTOSO/SHOP", "services/apps")
            ],
            [],
            InfrastructureDestinationId,
            CodeDestinationId,
            CodeDestinationId));

        Assert.True(configured.IsError);
        Assert.Equal("VAL-PUB-DESTINATION", Assert.Single(configured.Errors).Code);
    }
}
