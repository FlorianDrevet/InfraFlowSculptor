using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public sealed class EfIdempotencyStore(IfsDbContext dbContext, TimeProvider clock) : IIdempotencyStore
{
    public async Task<IdempotencyEntry?> FindAsync(
        OrganizationId organizationId,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.Key == key,
            cancellationToken);

        return entity is null ? null : ToEntry(entity);
    }

    public async Task<bool> TryStartAsync(
        OrganizationId organizationId,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.IdempotencyKeys.AnyAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.Key == key,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        var entity = new IdempotencyKeyEntity(
            organizationId,
            key,
            requestHash,
            clock.GetUtcNow());
        dbContext.IdempotencyKeys.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteAsync(
        OrganizationId organizationId,
        Guid key,
        IdempotencyResponse response,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.Key == key,
            cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException("The idempotency reservation no longer exists.");
        }

        entity.StatusCode = response.StatusCode;
        entity.Response = JsonSerializer.SerializeToElement(response);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        OrganizationId organizationId,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId && candidate.Key == key,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.IdempotencyKeys.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IdempotencyEntry ToEntry(IdempotencyKeyEntity entity)
    {
        var response = entity.Response is { } json
            ? JsonSerializer.Deserialize<IdempotencyResponse>(json.GetRawText())
            : null;

        if (entity.StatusCode is not null && response is not null && response.StatusCode != entity.StatusCode)
        {
            response = response with { StatusCode = entity.StatusCode.Value };
        }

        return new IdempotencyEntry(entity.OrganizationId, entity.Key, entity.RequestHash, response);
    }
}
