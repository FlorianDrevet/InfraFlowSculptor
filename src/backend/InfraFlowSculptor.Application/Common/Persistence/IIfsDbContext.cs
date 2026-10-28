using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Application.Common.Auditing;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIfsDbContext
{
    DbSet<TEntity> Query<TEntity>() where TEntity : class;

    IReadOnlyCollection<IDomainEvent> GetDomainEvents();

    void ClearDomainEvents();

    void ClearTrackedChanges();

    void AddOutboxMessage(OutboxMessageData message);

    IReadOnlyCollection<AuditEntityChange> GetAuditChanges();

    Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
