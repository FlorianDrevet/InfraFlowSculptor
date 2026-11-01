namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseAppOwnedStatePlan(
    string ResourceId,
    string Parameter,
    string Kind,
    string BootstrapImage);
