namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectValidationReadinessResponse(
    bool ModelValid,
    bool RevisionGenerable,
    bool Publishable,
    bool Deployable);
