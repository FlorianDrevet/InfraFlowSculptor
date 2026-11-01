using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Models;
using Mediator;
using NSubstitute;

namespace InfraFlowSculptor.Application.Tests.Common.Behaviors;

public sealed class UnitOfWorkFindingAcknowledgmentConflictTests
{
    [Fact]
    public async Task FindingAcknowledgmentUniqueViolationReturnsAlreadyAcknowledged()
    {
        var dbContext = Substitute.For<IIfsDbContext>();
        var transaction = Substitute.For<IIfsDbTransaction>();
        dbContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(transaction));
        dbContext.GetDomainEvents().Returns(Array.Empty<IDomainEvent>());
        dbContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new FindingAcknowledgmentConflictException(new InvalidOperationException()));

        var behavior = new UnitOfWorkBehavior<TestCommand, ErrorOr<Success>>(
            dbContext,
            Substitute.For<ICurrentOrganization>(),
            TimeProvider.System,
            Substitute.For<IAuditWriter>());
        var response = await behavior.Handle(
            new TestCommand(),
            (_, _) => ValueTask.FromResult<ErrorOr<Success>>(Result.Success),
            CancellationToken.None);

        Assert.True(response.IsError);
        Assert.Equal("FINDING_ALREADY_ACKNOWLEDGED", response.FirstError.Code);
        dbContext.Received(1).ClearDomainEvents();
        dbContext.Received(1).ClearTrackedChanges();
        await transaction.Received(1).RollbackAsync(CancellationToken.None);
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private sealed record TestCommand : ICommand<ErrorOr<Success>>;
}
