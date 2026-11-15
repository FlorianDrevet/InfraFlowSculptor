using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.GitConnectionAggregate;

public readonly record struct GitConnectionId(Guid Value) : IStronglyTypedId
{
    public static GitConnectionId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
