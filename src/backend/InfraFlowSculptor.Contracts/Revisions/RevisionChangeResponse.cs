namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionChangeResponse(
    string Category,
    string Subject,
    string DescriptionFr,
    string DescriptionEn);
