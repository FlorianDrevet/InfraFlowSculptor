namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct ComponentId(Guid Value) : IStronglyTypedId;
