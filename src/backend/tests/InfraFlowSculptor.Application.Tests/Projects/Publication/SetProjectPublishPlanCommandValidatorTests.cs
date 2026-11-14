using InfraFlowSculptor.Application.Projects.Publication;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Tests.Projects.Publication;

public sealed class SetProjectPublishPlanCommandValidatorTests
{
    private readonly SetProjectPublishPlanCommandValidator validator = new();

    [Fact]
    public void ValidateAcceptsAnEmptyMonoRepoPlanThatCanBeCompletedLater()
    {
        var result = validator.Validate(new SetProjectPublishPlanCommand(
            Guid.NewGuid(),
            PublishPreset.MonoRepo,
            [],
            [],
            null,
            null,
            null,
            1));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsAnAssignmentWithoutAComponentOrDestination()
    {
        var result = validator.Validate(new SetProjectPublishPlanCommand(
            Guid.NewGuid(),
            PublishPreset.MonoRepo,
            [],
            [new PublishPlanAssignmentInput(Guid.Empty, PublishPart.Infrastructure, Guid.Empty)],
            null,
            null,
            null,
            1));

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }
}
