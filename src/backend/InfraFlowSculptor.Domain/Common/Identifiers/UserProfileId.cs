namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct UserProfileId(Guid Value) : IStronglyTypedId
{
    public static UserProfileId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
