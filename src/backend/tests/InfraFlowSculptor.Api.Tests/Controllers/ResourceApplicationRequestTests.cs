using ErrorOr;
using InfraFlowSculptor.Api.Controllers;
using InfraFlowSculptor.Contracts.Resources;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Api.Tests.Controllers;

public sealed class ResourceApplicationRequestTests
{
    [Fact]
    public void NullStepReturnsValidationError()
    {
        var request = CreateRequest([null!]);

        AssertInvalidRequest(request);
    }

    [Fact]
    public void NullParameterReturnsValidationError()
    {
        var request = CreateRequest([
            new ApplicationStepRequest("ImageScan", true, true, "BeforeBuild", [null!])]);

        AssertInvalidRequest(request);
    }

    [Fact]
    public void NullStepCollectionReturnsValidationError()
    {
        var request = CreateRequest(null!);

        AssertInvalidRequest(request);
    }

    [Fact]
    public void EmptyStepCollectionIsAccepted()
    {
        var request = CreateRequest([]);

        var result = ResourcesController.ToApplicationDefinition(request);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Steps);
    }

    [Fact]
    public void NullParameterCollectionReturnsValidationError()
    {
        var request = CreateRequest([
            new ApplicationStepRequest("ImageScan", true, true, "BeforeBuild", null!)]);

        AssertInvalidRequest(request);
    }

    private static void AssertInvalidRequest(SaveApplicationRequest request)
    {
        ErrorOr<ApplicationDefinition> result = default;
        var exception = Record.Exception(() => result = ResourcesController.ToApplicationDefinition(request));
        Assert.Null(exception);
        Assert.True(result.IsError);
        Assert.Equal("APPLICATION_STEP_INVALID", result.Errors[0].Code);
    }

    private static SaveApplicationRequest CreateRequest(IReadOnlyList<ApplicationStepRequest> steps) => new(
        0,
        "orders-api",
        "src",
        "Dockerfile",
        ".",
        "orders/api",
        null,
        "Direct",
        null,
        steps);
}
