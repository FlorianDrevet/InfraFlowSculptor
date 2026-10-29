using InfraFlowSculptor.Application.Components.Commands.AcknowledgeFinding;
using InfraFlowSculptor.Application.Components.Commands.ApplyFindingFix;
using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Application.Tests.Components.Validation;

public sealed class FindingCommandValidatorTests
{
    [Fact]
    public void AcknowledgeFindingExposesNormalizedDetailKeyForLookup()
    {
        var command = new AcknowledgeFindingCommand(
            Guid.NewGuid(),
            "VAL-LIA-OBLIGATOIRE",
            ObjectType.Resource,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Reviewed",
            " Hosting ");

        Assert.Equal("Hosting", command.NormalizedDetailKey);
    }

    [Fact]
    public void ApplyFindingFixExposesNormalizedDetailKeyForLookup()
    {
        var command = new ApplyFindingFixCommand(
            Guid.NewGuid(),
            "VAL-LIA-OBLIGATOIRE",
            ObjectType.Resource,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            " Hosting ");

        Assert.Equal("Hosting", command.NormalizedDetailKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AcknowledgeFindingRejectsBlankDetailKeys(string detailKey)
    {
        var command = new AcknowledgeFindingCommand(
            Guid.NewGuid(),
            "VAL-LIA-OBLIGATOIRE",
            ObjectType.Resource,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Reviewed",
            detailKey);

        var result = new AcknowledgeFindingCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DetailKey));
    }

    [Fact]
    public void ApplyFindingFixRejectsOversizedDetailKeys()
    {
        var command = new ApplyFindingFixCommand(
            Guid.NewGuid(),
            "VAL-LIA-OBLIGATOIRE",
            ObjectType.Resource,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            new string('x', 129));

        var result = new ApplyFindingFixCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DetailKey));
    }
}
