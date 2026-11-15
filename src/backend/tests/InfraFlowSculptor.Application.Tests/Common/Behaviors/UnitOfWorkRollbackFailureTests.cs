using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using Mediator;
using NSubstitute;

namespace InfraFlowSculptor.Application.Tests.Common.Behaviors;

public sealed class UnitOfWorkRollbackFailureTests
{
    [Fact]
    public async Task ErrorResponseRollsBackEvenWhenTheRequestWasCancelled()
    {
        var dbContext = Substitute.For<IIfsDbContext>();
        var transaction = Substitute.For<IIfsDbTransaction>();
        CancellationToken rollbackToken = default;
        dbContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIfsDbTransaction>(transaction));
        transaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                rollbackToken = call.Arg<CancellationToken>();
                return Task.CompletedTask;
            });
        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<Success>>(
            dbContext,
            Substitute.For<ICurrentOrganization>(),
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<Success>>(Error.Validation("INVALID", "Invalid command.")),
            cancellation.Token);

        Assert.True(response.IsError);
        Assert.Equal(CancellationToken.None, rollbackToken);
    }

    [Fact]
    public async Task CommandFailureRemainsPrimaryWhenTransactionRollbackAlsoFails()
    {
        var dbContext = Substitute.For<IIfsDbContext>();
        var transaction = Substitute.For<IIfsDbTransaction>();
        var commandFailure = new InvalidOperationException("Command failed.");
        var rollbackFailure = new InvalidOperationException("Rollback failed.");
        dbContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIfsDbTransaction>(transaction));
        transaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(rollbackFailure));
        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<Success>>(
            dbContext,
            Substitute.For<ICurrentOrganization>(),
            TimeProvider.System,
            Substitute.For<IAuditWriter>());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new TestCommand(),
                (_, _) => ValueTask.FromException<ErrorOr<Success>>(commandFailure),
                CancellationToken.None));

        Assert.Same(commandFailure, actual);
        Assert.Contains(rollbackFailure, actual.Data.Values.Cast<Exception>());
    }

    [Fact]
    public async Task FailedConcurrencyRollbackClearsTrackedChangesBeforeRethrowing()
    {
        var dbContext = Substitute.For<IIfsDbContext>();
        var transaction = Substitute.For<IIfsDbTransaction>();
        var commandFailure = new ConcurrencyConflictException(new InvalidOperationException("Concurrency failure."));
        dbContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIfsDbTransaction>(transaction));
        transaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("Rollback failed.")));
        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<Success>>(
            dbContext,
            Substitute.For<ICurrentOrganization>(),
            TimeProvider.System,
            Substitute.For<IAuditWriter>());

        var actual = await Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await behavior.Handle(
                new TestCommand(),
                (_, _) => ValueTask.FromException<ErrorOr<Success>>(commandFailure),
                CancellationToken.None));

        Assert.Same(commandFailure, actual);
        dbContext.Received(1).ClearDomainEvents();
        dbContext.Received(1).ClearTrackedChanges();
    }

    private sealed record TestCommand : ICommand<ErrorOr<Success>>;
}
