namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct InvitationId(Guid Value) : IStronglyTypedId
{
    public static InvitationId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
