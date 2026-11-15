using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class IfsDbTransactionTests
{
    [Fact]
    public async Task DisposeDoesNotRetryRollbackAfterSqlRollbackFailure()
    {
        var sqlTransaction = Substitute.For<IDbContextTransaction>();
        var rollbackAttempts = 0;
        sqlTransaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                rollbackAttempts++;
                return Task.FromException(new InvalidOperationException("Rollback failed."));
            });
        var transaction = new IfsDbTransaction(sqlTransaction, true, null, null, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.RollbackAsync());
        await transaction.DisposeAsync();

        Assert.Equal(1, rollbackAttempts);
    }

    [Fact]
    public async Task RollbackFailureIsNotRetriedByTheSameTransactionWrapper()
    {
        var sqlTransaction = Substitute.For<IDbContextTransaction>();
        var rollbackAttempts = 0;
        sqlTransaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                rollbackAttempts++;
                return Task.FromException(new InvalidOperationException("Rollback failed."));
            });
        var transaction = new IfsDbTransaction(sqlTransaction, true, null, null, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.RollbackAsync());
        await transaction.RollbackAsync();

        Assert.Equal(1, rollbackAttempts);
    }

    [Fact]
    public async Task CommitFailureDoesNotRunRollbackCleanupOrIssueExplicitRollback()
    {
        var sqlTransaction = Substitute.For<IDbContextTransaction>();
        sqlTransaction.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("Commit outcome is uncertain.")));
        var cleanupExecuted = false;
        var rollbackActions = new InfraFlowSculptor.Application.Common.Persistence.TransactionRollbackActions(
            NullLogger<InfraFlowSculptor.Application.Common.Persistence.TransactionRollbackActions>.Instance);
        rollbackActions.Register(_ =>
        {
            cleanupExecuted = true;
            return Task.CompletedTask;
        });
        var transaction = new IfsDbTransaction(sqlTransaction, true, null, rollbackActions, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
        await transaction.DisposeAsync();

        Assert.False(cleanupExecuted);
        await sqlTransaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}
