namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct OrganizationId(Guid Value) : IStronglyTypedId
{
    public static OrganizationId New(TimeProvider clock) =>
        new(Guid.CreateVersion7(clock.GetUtcNow()));
}
