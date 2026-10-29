namespace InfraFlowSculptor.Contracts.Projects;

public sealed record FindingAcknowledgmentResponse(
    string Comment,
    Guid AcknowledgedBy,
    string AcknowledgedByDisplayName,
    DateTimeOffset CreatedAt);
