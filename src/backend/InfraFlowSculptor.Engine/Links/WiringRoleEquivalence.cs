namespace InfraFlowSculptor.Engine.Links;

public sealed record WiringRoleEquivalence(
    Guid TargetResourceId,
    string RequestedRole,
    string SatisfiedByRole,
    WiringOrigin RequestOrigin,
    WiringOrigin ExistingOrigin);
