namespace InfraFlowSculptor.Engine.Validation;

public sealed record ValidationReadiness(
    bool ModelValid,
    bool RevisionGenerable,
    bool Publishable,
    bool Deployable);
