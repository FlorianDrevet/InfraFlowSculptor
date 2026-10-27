using System.Text.Json;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public sealed class EfIdempotencyStore(IfsDbContext dbContext, TimeProvider clock) : IIdempotencyStore
{
    public Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        dbContext.BeginTransactionAsync(cancellationToken);

    public async Task<IdempotencyEntry?> FindAsync(
        IdempotencyScope scope,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        if (scope.OrganizationId is { } organizationId)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.Key == storageKey,
                cancellationToken);
            return entity is null ? null : ToEntry(entity.RequestHash, entity.StatusCode, entity.Response);
        }

        if (scope.OrganizationId is null)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.UserIdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.TenantId == scope.UserKey.TenantId
                    && candidate.UserObjectId == scope.UserKey.ObjectId
                    && candidate.Key == storageKey,
                cancellationToken);
            return entity is null ? null : ToEntry(entity.RequestHash, entity.StatusCode, entity.Response);
        }

        throw new ArgumentException("A valid idempotency scope is required.", nameof(scope));
    }

    public async Task<bool> TryStartAsync(
        IdempotencyScope scope,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        if (scope.OrganizationId is { } organizationId)
        {
            var storageKey = scope.ToStorageKey(key);
            var exists = await dbContext.IdempotencyKeys.AnyAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.Key == storageKey,
                cancellationToken);
            if (exists)
            {
                return false;
            }

            return await SaveReservationAsync(
                new IdempotencyKeyEntity(organizationId, storageKey, requestHash, clock.GetUtcNow()),
                cancellationToken);
        }

        if (scope.OrganizationId is null)
        {
            var storageKey = scope.ToStorageKey(key);
            var exists = await dbContext.UserIdempotencyKeys.AnyAsync(
                candidate => candidate.TenantId == scope.UserKey.TenantId
                    && candidate.UserObjectId == scope.UserKey.ObjectId
                    && candidate.Key == storageKey,
                cancellationToken);
            if (exists)
            {
                return false;
            }

            return await SaveReservationAsync(
                new UserIdempotencyKeyEntity(scope.UserKey, storageKey, requestHash, clock.GetUtcNow()),
                cancellationToken);
        }

        throw new ArgumentException("A valid idempotency scope is required.", nameof(scope));
    }

    public async Task CompleteAsync(
        IdempotencyScope scope,
        Guid key,
        IdempotencyResponse response,
        CancellationToken cancellationToken = default)
    {
        if (scope.OrganizationId is { } organizationId)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.Key == storageKey,
                cancellationToken);
            Complete(entity, response);
        }
        else if (scope.OrganizationId is null)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.UserIdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.TenantId == scope.UserKey.TenantId
                    && candidate.UserObjectId == scope.UserKey.ObjectId
                    && candidate.Key == storageKey,
                cancellationToken);
            Complete(entity, response);
        }
        else
        {
            throw new ArgumentException("A valid idempotency scope is required.", nameof(scope));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        IdempotencyScope scope,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        if (scope.OrganizationId is { } organizationId)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.IdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.Key == storageKey,
                cancellationToken);
            if (entity is not null)
            {
                dbContext.IdempotencyKeys.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (scope.OrganizationId is null)
        {
            var storageKey = scope.ToStorageKey(key);
            var entity = await dbContext.UserIdempotencyKeys.SingleOrDefaultAsync(
                candidate => candidate.TenantId == scope.UserKey.TenantId
                    && candidate.UserObjectId == scope.UserKey.ObjectId
                    && candidate.Key == storageKey,
                cancellationToken);
            if (entity is not null)
            {
                dbContext.UserIdempotencyKeys.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        throw new ArgumentException("A valid idempotency scope is required.", nameof(scope));
    }

    private async Task<bool> SaveReservationAsync<TEntity>(
        TEntity entity,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        dbContext.Add(entity);

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

    private static void Complete<TEntity>(TEntity? entity, IdempotencyResponse response)
        where TEntity : class
    {
        if (entity is IdempotencyKeyEntity organizationEntity)
        {
            organizationEntity.StatusCode = response.StatusCode;
            organizationEntity.Response = JsonSerializer.SerializeToElement(response);
            return;
        }

        if (entity is UserIdempotencyKeyEntity userEntity)
        {
            userEntity.StatusCode = response.StatusCode;
            userEntity.Response = JsonSerializer.SerializeToElement(response);
            return;
        }

        throw new InvalidOperationException("The idempotency reservation no longer exists.");
    }

    private static IdempotencyEntry ToEntry(string requestHash, int? statusCode, JsonElement? json)
    {
        var response = json is { } storedJson
            ? JsonSerializer.Deserialize<IdempotencyResponse>(storedJson.GetRawText())
            : null;

        if (statusCode is not null && response is not null && response.StatusCode != statusCode)
        {
            response = response with { StatusCode = statusCode.Value };
        }

        return new IdempotencyEntry(requestHash, response);
    }
}
