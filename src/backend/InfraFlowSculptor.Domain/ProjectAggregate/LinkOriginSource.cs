namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed record LinkOriginSource(LinkOriginSourceKind Kind, Guid? Id, string? Name);
