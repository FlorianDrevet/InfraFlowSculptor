namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct PublicationId(Guid Value) : IStronglyTypedId
{
    public static PublicationId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
