using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkOriginSourceResponse(LinkOriginSourceKind Kind, Guid? Id, string? Name);
