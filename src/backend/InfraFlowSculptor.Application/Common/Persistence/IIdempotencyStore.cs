using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> FindAsync(
        OrganizationId organizationId,
        Guid key,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartAsync(
        OrganizationId organizationId,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        OrganizationId organizationId,
        Guid key,
        IdempotencyResponse response,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        OrganizationId organizationId,
        Guid key,
        CancellationToken cancellationToken = default);
}
