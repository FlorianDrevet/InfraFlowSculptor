using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public class IfsDbContext(
    DbContextOptions options,
    ICurrentOrganization currentOrganization)
    : DbContext(options), IIfsDbContext
{
    private readonly ICurrentOrganization currentOrganization = currentOrganization;

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();

    public DbSet<ScheduledJobLeaseEntity> ScheduledJobLeases => Set<ScheduledJobLeaseEntity>();

    public DbSet<ProcessedJobEntity> ProcessedJobs => Set<ProcessedJobEntity>();

    public DbSet<OrganizationJobBudgetEntity> OrganizationJobBudgets => Set<OrganizationJobBudgetEntity>();

    public OrganizationId? CurrentOrganizationId => currentOrganization.Id;

    DbSet<TEntity> IIfsDbContext.Query<TEntity>() => Set<TEntity>();

    public async Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new IfsDbTransaction(transaction);
    }

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() =>
        ChangeTracker.Entries<IHasDomainEvents>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();

    public void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries<IHasDomainEvents>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }

    public void AddOutboxMessage(OutboxMessageData message) =>
        OutboxMessages.Add(new OutboxMessageEntity(message));

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        IncrementVersions();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        var idTypes = typeof(IStronglyTypedId).Assembly.GetTypes()
            .Where(type => type.IsValueType
                && !type.IsAbstract
                && typeof(IStronglyTypedId).IsAssignableFrom(type));

        foreach (var idType in idTypes)
        {
            var converterType = typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IfsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var entityBuilder = modelBuilder.Entity(entityType.ClrType);

            if (typeof(IOrganizationOwned).IsAssignableFrom(entityType.ClrType))
            {
                var entity = Expression.Parameter(entityType.ClrType, "entity");
                var organizationId = Expression.Convert(
                    Expression.Property(
                        Expression.Convert(entity, typeof(IOrganizationOwned)),
                        nameof(IOrganizationOwned.OrganizationId)),
                    typeof(OrganizationId?));
                var currentOrganizationId = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentOrganizationId));
                var filter = Expression.Lambda(
                    Expression.Equal(organizationId, currentOrganizationId),
                    entity);

                entityBuilder.HasQueryFilter(filter);
            }

            if (typeof(IHasVersion).IsAssignableFrom(entityType.ClrType))
            {
                entityBuilder.Property(nameof(IHasVersion.Version)).IsConcurrencyToken();
            }
        }
    }

    private void IncrementVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IHasVersion>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            var version = entry.Property(nameof(IHasVersion.Version));
            version.CurrentValue = checked((int)version.OriginalValue! + 1);
        }
    }
}
