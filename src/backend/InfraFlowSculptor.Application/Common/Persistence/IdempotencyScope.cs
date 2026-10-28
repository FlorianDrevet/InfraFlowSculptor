using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed record IdempotencyScope
{
    private IdempotencyScope(OrganizationId? organizationId, UserKey userKey)
    {
        if (userKey.IsEmpty)
        {
            throw new ArgumentException("An authenticated user key is required.", nameof(userKey));
        }

        if (organizationId is { Value: var organizationValue } && organizationValue == Guid.Empty)
        {
            throw new ArgumentException("A valid organization identifier is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        UserKey = userKey;
    }

    public OrganizationId? OrganizationId { get; }

    public UserKey UserKey { get; }

    public static IdempotencyScope ForOrganization(OrganizationId organizationId, UserKey userKey)
    {
        return new IdempotencyScope(organizationId, userKey);
    }

    public static IdempotencyScope ForUser(UserKey userKey)
    {
        return new IdempotencyScope(null, userKey);
    }

    public Guid ToStorageKey(Guid clientKey)
    {
        if (OrganizationId is null)
        {
            return clientKey;
        }

        var scopeIdentity = Encoding.UTF8.GetBytes(
            $"{UserKey.TenantId:N}:{UserKey.ObjectId:N}:{clientKey:N}");
        return new Guid(SHA256.HashData(scopeIdentity).AsSpan(0, 16));
    }
}
