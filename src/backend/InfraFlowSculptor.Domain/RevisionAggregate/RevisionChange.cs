namespace InfraFlowSculptor.Domain.RevisionAggregate;

public sealed record RevisionChange(
    string Category,
    string Subject,
    string DescriptionFr,
    string DescriptionEn);
