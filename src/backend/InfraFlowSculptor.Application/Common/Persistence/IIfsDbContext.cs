using InfraFlowSculptor.Domain.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIfsDbContext
{
    DbSet<TEntity> Query<TEntity>() where TEntity : class;

    IReadOnlyCollection<IDomainEvent> GetDomainEvents();

    void ClearDomainEvents();

    void AddOutboxMessage(OutboxMessageData message);

    Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
