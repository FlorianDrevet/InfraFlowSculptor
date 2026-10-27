using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using InfraFlowSculptor.Infrastructure.Persistence.Identity;
using InfraFlowSculptor.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public class IfsDbContext(
    DbContextOptions options,
    ICurrentOrganization currentOrganization)
    : DbContext(options), IIfsDbContext
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ICurrentOrganization currentOrganization = currentOrganization;

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();

    public DbSet<ScheduledJobLeaseEntity> ScheduledJobLeases => Set<ScheduledJobLeaseEntity>();

    public DbSet<ProcessedJobEntity> ProcessedJobs => Set<ProcessedJobEntity>();

    public DbSet<OrganizationJobBudgetEntity> OrganizationJobBudgets => Set<OrganizationJobBudgetEntity>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<OrganizationAggregate> Organizations => Set<OrganizationAggregate>();

    public DbSet<InvitationAggregate> Invitations => Set<InvitationAggregate>();

    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    public DbSet<AuditPersonalDataEntity> AuditPersonalData => Set<AuditPersonalDataEntity>();

    public DbSet<OrganizationFeatureAccessEntity> OrganizationFeatureAccess => Set<OrganizationFeatureAccessEntity>();

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

    public IReadOnlyCollection<AuditEntityChange> GetAuditChanges()
    {
        var changes = new List<AuditEntityChange>();
        foreach (var entry in ChangeTracker.Entries())
        {
            var auditObject = entry.Metadata.ClrType.GetCustomAttribute<AuditedObjectAttribute>();
            if (auditObject is null
                || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var objectId = ReadGuid(entry.Property(auditObject.IdProperty).CurrentValue);
            var organizationId = ReadGuid(entry.Property(auditObject.OrganizationIdProperty).CurrentValue);
            var primaryKeyNames = entry.Metadata.FindPrimaryKey()?.Properties
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal) ?? [];
            var properties = entry.Properties
                .Where(property => !primaryKeyNames.Contains(property.Metadata.Name)
                    && property.Metadata.Name != nameof(IHasVersion.Version)
                    && (entry.State != EntityState.Modified || property.IsModified))
                .Select(property => new AuditPropertyChange(
                    property.Metadata.Name,
                    entry.State == EntityState.Added
                        ? null
                        : ToJsonElement(property.OriginalValue, entry.Metadata.ClrType, property.Metadata.Name),
                    entry.State == EntityState.Deleted
                        ? null
                        : ToJsonElement(property.CurrentValue, entry.Metadata.ClrType, property.Metadata.Name),
                    entry.Metadata.ClrType
                        .GetProperty(property.Metadata.Name)
                        ?.GetCustomAttribute<PersonalDataAttribute>()
                        ?.Field))
                .ToArray();

            changes.Add(new AuditEntityChange(
                auditObject.ObjectType,
                objectId,
                organizationId,
                properties));
        }

        return changes;
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
            if (entityType.IsOwned())
            {
                continue;
            }

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

    private static Guid ReadGuid(object? value) => value switch
    {
        Guid guid => guid,
        IStronglyTypedId stronglyTypedId => stronglyTypedId.Value,
        _ => throw new InvalidOperationException("An audited entity must have a stable GUID identifier.")
    };

    private static JsonElement? ToJsonElement(object? value, Type entityType, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        if (entityType == typeof(OrganizationMember)
            && propertyName == nameof(OrganizationMember.RoleValues)
            && value is int[] roleValues)
        {
            var roles = roleValues.Select(role => (OrganizationRole)role).ToArray();
            return JsonSerializer.SerializeToElement(roles, AuditJsonOptions);
        }

        return JsonSerializer.SerializeToElement(value, value.GetType(), AuditJsonOptions);
    }
}
