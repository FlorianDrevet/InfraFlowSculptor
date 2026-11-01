namespace InfraFlowSculptor.Application.Components.Queries.GetProjectValidation;

public sealed record FindingAcknowledgmentSummary(
    string Comment,
    Guid AcknowledgedBy,
    string AcknowledgedByDisplayName,
    DateTimeOffset CreatedAt);
