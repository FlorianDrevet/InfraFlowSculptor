namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct RevisionId(Guid Value) : IStronglyTypedId
{
    public static RevisionId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
