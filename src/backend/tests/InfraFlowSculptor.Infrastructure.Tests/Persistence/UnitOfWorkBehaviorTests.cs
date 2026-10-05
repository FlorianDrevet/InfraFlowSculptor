using ErrorOr;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;
using Mediator;

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
            TimeProvider.System);
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
            TimeProvider.System);
        var failure = Error.Validation("TEST_FAILURE", "The command failed.");
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<string>>(failure),
            CancellationToken.None);

        Assert.True(response.IsError);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
        Assert.Empty(context.GetDomainEvents());
    }

    private sealed record TestCommand : ICommand<ErrorOr<string>>;

    private sealed class TestCurrentOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
