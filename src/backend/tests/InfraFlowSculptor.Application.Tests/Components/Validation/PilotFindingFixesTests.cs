using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Tests.Components.Validation;

public sealed class PilotFindingFixesTests
{
    [Fact]
    public void RestrictedExposureFindingDoesNotOfferAnAutomaticFix()
    {
        var registry = new FindingFixRegistry(
            PilotFindingFixes.Create(new LinkKindRegistry(PilotLinkKindHandlers.Create())));

        Assert.False(registry.Supports("VAL-NET-IP-VIDE"));
    }
}
