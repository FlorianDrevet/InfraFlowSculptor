using InfraFlowSculptor.Application.Common.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraFlowSculptor.Application.Tests.Common.Persistence;

public sealed class TransactionRollbackActionsTests
{
    [Fact]
    public async Task NestedRollbackRunsOnlyActionsRegisteredAfterItsCheckpoint()
    {
        var actions = new TransactionRollbackActions(NullLogger<TransactionRollbackActions>.Instance);
        var executed = new List<string>();
        actions.Register(_ =>
        {
            executed.Add("outer");
            return Task.CompletedTask;
        });
        var checkpoint = actions.CaptureCheckpoint();
        actions.Register(_ =>
        {
            executed.Add("inner");
            return Task.CompletedTask;
        });

        await actions.RollbackAsync(checkpoint, CancellationToken.None);

        Assert.Equal(["inner"], executed);
        await actions.RollbackAsync(0, CancellationToken.None);
        Assert.Equal(["inner", "outer"], executed);
    }

    [Fact]
    public async Task CommitClearsRollbackActions()
    {
        var actions = new TransactionRollbackActions(NullLogger<TransactionRollbackActions>.Instance);
        var rollbackExecuted = false;
        actions.Register(_ =>
        {
            rollbackExecuted = true;
            return Task.CompletedTask;
        });

        actions.Commit();
        await actions.RollbackAsync(0, CancellationToken.None);

        Assert.False(rollbackExecuted);
    }
}
