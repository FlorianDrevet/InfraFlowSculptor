namespace InfraFlowSculptor.Domain.Common.Identifiers;

public interface IStronglyTypedId
{
    Guid Value { get; }
}
