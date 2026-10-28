namespace InfraFlowSculptor.Domain.Common.Identifiers;

public readonly record struct UserKey(Guid TenantId, Guid ObjectId)
{
    public bool IsEmpty => TenantId == Guid.Empty || ObjectId == Guid.Empty;
}
