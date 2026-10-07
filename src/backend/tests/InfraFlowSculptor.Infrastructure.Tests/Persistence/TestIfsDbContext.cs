using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

internal sealed class TestIfsDbContext(
    DbContextOptions<TestIfsDbContext> options,
    ICurrentOrganization currentOrganization)
    : IfsDbContext(options, currentOrganization)
{
    public DbSet<OrganizationOwnedTestEntity> OrganizationOwnedEntities => Set<OrganizationOwnedTestEntity>();

    public DbSet<TestAggregate> TestAggregates => Set<TestAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationOwnedTestEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<TestAggregate>().HasKey(aggregate => aggregate.Id);
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class TestAggregate : AggregateRoot<Guid>, IOrganizationOwned, IHasVersion
{
    private TestAggregate()
    {
    }

    public TestAggregate(Guid id, OrganizationId organizationId, string name)
        : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
    }

    public OrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public void RaiseTestEvent() => RaiseDomainEvent(new TestDomainEvent(Name));

    private sealed record TestDomainEvent(string Name) : IDomainEvent;
}

internal sealed class OrganizationOwnedTestEntity : IOrganizationOwned, IHasVersion
{
    public Guid Id { get; set; }

    public OrganizationId OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }
}
