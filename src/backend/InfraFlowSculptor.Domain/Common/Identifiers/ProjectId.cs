namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct ProjectId(Guid Value) : IStronglyTypedId
{
    public static ProjectId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
