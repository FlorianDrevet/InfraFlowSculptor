using ErrorOr;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Mediator;
using NSubstitute;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class UnitOfWorkBehaviorTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task SuccessfulCommandWritesDomainEventsToTheOutbox()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var context = postgres.CreateDbContext(currentOrganization);
        await context.Database.EnsureCreatedAsync();

        var aggregate = new TestAggregate(Guid.NewGuid(), organizationId, "created");
        context.TestAggregates.Add(aggregate);
        await context.SaveChangesAsync();
        aggregate.RaiseTestEvent();

        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<string>>(
            context,
            currentOrganization,
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<string>>("complete"),
            CancellationToken.None);

        Assert.False(response.IsError);
        var outboxMessage = await context.OutboxMessages.SingleAsync();
        Assert.Equal(string.Empty, outboxMessage.Queue);
        Assert.Equal(organizationId.Value.ToString("D"), outboxMessage.SessionId);
        Assert.Contains("TestDomainEvent", outboxMessage.Type, StringComparison.Ordinal);
        Assert.Empty(context.GetDomainEvents());
    }

    [Fact]
    public async Task FailedCommandDoesNotWriteDomainEventsToTheOutbox()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var context = postgres.CreateDbContext(currentOrganization);
        await context.Database.EnsureCreatedAsync();

        var aggregate = new TestAggregate(Guid.NewGuid(), organizationId, "failed");
        context.TestAggregates.Add(aggregate);
        await context.SaveChangesAsync();
        aggregate.RaiseTestEvent();

        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<string>>(
            context,
            currentOrganization,
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        var failure = Error.Validation("TEST_FAILURE", "The command failed.");
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<string>>(failure),
            CancellationToken.None);

        Assert.True(response.IsError);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
        Assert.Empty(context.GetDomainEvents());
    }

    [Fact]
    public async Task UnitOfWorkDoesNotCommitTransactionOwnedByIdempotencyFilter()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var currentOrganization = new TestCurrentOrganization(organizationId);
        await using var context = postgres.CreateDbContext(currentOrganization);
        await context.Database.EnsureCreatedAsync();

        var aggregate = new TestAggregate(Guid.NewGuid(), organizationId, "created");
        context.TestAggregates.Add(aggregate);
        await context.SaveChangesAsync();
        aggregate.RaiseTestEvent();

        await using var outerTransaction = await context.BeginTransactionAsync();
        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<string>>(
            context,
            currentOrganization,
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<string>>("complete"),
            CancellationToken.None);

        Assert.False(response.IsError);
        Assert.NotNull(context.Database.CurrentTransaction);
        await using (var observer = postgres.CreateDbContext(currentOrganization))
        {
            Assert.Empty(await observer.OutboxMessages.ToListAsync());
        }

        await outerTransaction.CommitAsync();
        await using var committedContext = postgres.CreateDbContext(currentOrganization);
        Assert.Single(await committedContext.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task MembershipConflictRollsBackToSavepointBeforeIdempotencyResponseCommits()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var owner = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Organization Owner",
            "owner@example.test",
            DateTimeOffset.UtcNow).Value;
        var invitee = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Concurrent Invitee",
            "invitee@example.test",
            DateTimeOffset.UtcNow).Value;
        var currentOrganization = new TestCurrentOrganization(organizationId);
        var organization = OrganizationAggregate.Create(
            organizationId,
            "Savepoint Test",
            owner.Id).Value;

        await using var context = postgres.CreateDbContext(currentOrganization);
        await context.Database.EnsureCreatedAsync();
        context.UserProfiles.AddRange(owner, invitee);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        var idempotencyStore = new EfIdempotencyStore(context, TimeProvider.System);
        var scope = IdempotencyScope.ForOrganization(organizationId, invitee.UserKey);
        var key = Guid.NewGuid();
        await using var outerTransaction = await context.BeginTransactionAsync();
        Assert.True(await idempotencyStore.TryStartAsync(scope, key, new string('a', 64)));

        var reloadedOrganization = await context.Organizations
            .SingleAsync(candidate => candidate.Id == organizationId);
        await using (var competingContext = postgres.CreateDbContext(currentOrganization))
        {
            await competingContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO organization_members (organization_id, user_profile_id, roles) VALUES ({0}, {1}, ARRAY[{2}])",
                organizationId.Value,
                invitee.Id.Value,
                (int)OrganizationRole.Member);
        }

        Assert.False(reloadedOrganization.AddMember(invitee.Id, [OrganizationRole.Member]).IsError);

        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<string>>(
            context,
            currentOrganization,
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<string>>("accepted"),
            CancellationToken.None);

        Assert.True(response.IsError);
        Assert.Equal("CONFLICT_VERSION", response.FirstError.Code);

        await idempotencyStore.CompleteAsync(
            scope,
            key,
            new IdempotencyResponse(
                409,
                "application/problem+json",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                "{\"code\":\"CONFLICT_VERSION\"}"u8.ToArray()));
        await outerTransaction.CommitAsync();

        await using var observer = postgres.CreateDbContext(currentOrganization);
        var committedResponse = await new EfIdempotencyStore(observer, TimeProvider.System).FindAsync(scope, key);
        Assert.Equal(409, committedResponse?.Response?.StatusCode);
    }

    private sealed record TestCommand : ICommand<ErrorOr<string>>;

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
