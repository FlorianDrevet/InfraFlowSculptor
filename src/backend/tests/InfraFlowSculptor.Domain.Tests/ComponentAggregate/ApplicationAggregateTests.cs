using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.ComponentAggregate;

public sealed class ApplicationAggregateTests
{
    private static readonly ProjectId ProjectId = new(Guid.NewGuid());
    private static readonly OrganizationId OrganizationId = new(Guid.NewGuid());
    private static readonly ComponentId ComponentId = new(Guid.NewGuid());

    [Fact]
    public void ValidContainerApplicationStoresNormalizedPathsAndPipelineSteps()
    {
        var result = Create(Definition());

        Assert.False(result.IsError);
        Assert.Equal("api", result.Value.Name);
        Assert.Equal("src/api", result.Value.CodeSourcePath);
        Assert.Equal("src/api/Dockerfile", result.Value.DockerfilePath);
        Assert.Equal("shop/orders/api", result.Value.ImageRepository);
        Assert.Equal("CRITICAL", Assert.Single(result.Value.Steps).Parameters.Single().Value);
    }

    [Fact]
    public void ApplicationNameIsUniqueWithinItsComponent()
    {
        var existing = Create(Definition()).Value;

        var duplicate = ApplicationAggregate.Create(
            Guid.NewGuid(), ProjectId, OrganizationId, ComponentId, Guid.NewGuid(), Definition(), [existing]);

        Assert.True(duplicate.IsError);
        Assert.Equal("APPLICATION_NAME_DUPLICATE", Assert.Single(duplicate.Errors).Code);
    }

    [Theory]
    [InlineData("../outside", "APPLICATION_PATH_INVALID")]
    [InlineData("C:/outside", "APPLICATION_PATH_INVALID")]
    [InlineData("shop/Orders/api", "APPLICATION_IMAGE_REPOSITORY_INVALID")]
    public void InvalidContainerApplicationPathsAndImageRepositoriesAreRejected(string value, string expectedCode)
    {
        var definition = expectedCode == "APPLICATION_IMAGE_REPOSITORY_INVALID"
            ? Definition() with { ImageRepository = value }
            : Definition() with { CodeSourcePath = value };

        var result = Create(definition);

        Assert.True(result.IsError);
        Assert.Equal(expectedCode, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void RelativeApplicationPathRejectsControlCharacters()
    {
        var result = Create(Definition() with { CodeSourcePath = "src/\napi" });

        Assert.True(result.IsError);
        Assert.Equal("APPLICATION_PATH_INVALID", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void HealthCheckPathRejectsControlCharacters()
    {
        var result = Create(Definition() with { HealthCheckPath = "/health\nready" });

        Assert.True(result.IsError);
        Assert.Equal("APPLICATION_HEALTH_CHECK_PATH_INVALID", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void StepNameRejectsTrailingLineFeed()
    {
        var result = Create(Definition() with
        {
            Steps = [new ApplicationStepDefinition(
                "ImageScan\n", true, true, ApplicationStepPosition.AfterBuild,
                [new ApplicationStepParameter("minimumSeverity", "CRITICAL")])]
        });

        Assert.True(result.IsError);
        Assert.Equal("APPLICATION_STEP_INVALID", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void StepParameterNameRejectsTrailingLineFeed()
    {
        var result = Create(Definition() with
        {
            Steps = [new ApplicationStepDefinition(
                "ImageScan", true, true, ApplicationStepPosition.AfterBuild,
                [new ApplicationStepParameter("minimumSeverity\n", "CRITICAL")])]
        });

        Assert.True(result.IsError);
        Assert.Equal("APPLICATION_STEP_PARAMETER_INVALID", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void UpdatingApplicationAdvancesItsOptimisticConcurrencyVersion()
    {
        var application = Create(Definition()).Value;

        var result = application.Update(Definition() with { HealthCheckPath = "/health/ready" });

        Assert.False(result.IsError);
        Assert.Equal(2, application.Version);
        Assert.Equal("/health/ready", application.HealthCheckPath);
    }

    private static ErrorOr<ApplicationAggregate> Create(ApplicationDefinition definition) =>
        ApplicationAggregate.Create(
            Guid.NewGuid(), ProjectId, OrganizationId, ComponentId, Guid.NewGuid(), definition);

    private static ApplicationDefinition Definition() => new(
        "api",
        "src/api",
        "src/api/Dockerfile",
        "src/api",
        "shop/orders/api",
        null,
        DeploymentStrategyKind.Direct,
        null,
        [new ApplicationStepDefinition(
            "ImageScan",
            true,
            true,
            ApplicationStepPosition.AfterBuild,
            [new ApplicationStepParameter("minimumSeverity", "CRITICAL")])]);
}
