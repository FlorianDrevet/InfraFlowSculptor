using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Projects.Commands.CreateProject;
using Mediator;
using NSubstitute;

namespace InfraFlowSculptor.Application.Tests.Common.ModelCommands;

[SuppressMessage("Reliability", "CA2012", Justification = "NSubstitute uses ValueTask-returning calls as setup specifications.")]
public sealed class ModelCommandExecutorTests
{
    [Fact]
    public async Task DryRunRollsBackTheSavepointAndReturnsThePreview()
    {
        var (executor, transaction, sender) = CreateExecutor();
        sender.Send(Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ModelCommandOutcome>>(
                new ModelCommandOutcome("project created", ["review this project"])));

        var result = await executor.ExecuteAsync(
            [CreateCommand(Guid.NewGuid())],
            ModelCommandMode.DryRun);

        Assert.False(result.IsError);
        Assert.Equal(ModelCommandMode.DryRun, result.Value.Mode);
        Assert.Equal(["project created"], result.Value.Effects);
        Assert.Equal(["review this project"], result.Value.Findings);
        await transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailureInLaterCommandRollsBackTheWholeList()
    {
        var (executor, transaction, sender) = CreateExecutor();
        sender.Send(Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(), Arg.Any<CancellationToken>())
            .Returns(
                new ValueTask<ErrorOr<ModelCommandOutcome>>(new ModelCommandOutcome("first applied", [])),
                new ValueTask<ErrorOr<ModelCommandOutcome>>(Error.Validation("SECOND_FAILED", "The second command failed.")));

        var projectId = Guid.NewGuid();
        var result = await executor.ExecuteAsync(
            [CreateCommand(projectId), CreateCommand(projectId)],
            ModelCommandMode.Apply);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "SECOND_FAILED");
        await transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await sender.Received(2).Send(
            Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyReleasesItsSavepointForAnOuterIdempotencyTransaction()
    {
        var (executor, transaction, sender) = CreateExecutor(isOwner: false);
        sender.Send(Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<ModelCommandOutcome>>(new ModelCommandOutcome("created", [])));

        var result = await executor.ExecuteAsync([CreateCommand(Guid.NewGuid())], ModelCommandMode.Apply);

        Assert.False(result.IsError);
        await transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndefinedModeIsRejectedBeforeDispatchingCommands()
    {
        var (executor, _, sender) = CreateExecutor();

        var result = await executor.ExecuteAsync(
            [CreateCommand(Guid.NewGuid())],
            (ModelCommandMode)99);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "MODEL_COMMAND_MODE_INVALID");
        await sender.DidNotReceive().Send(
            Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyProjectWorkspaceIsRejectedBeforeDispatchingCommands()
    {
        var (executor, _, sender) = CreateExecutor();

        var result = await executor.ExecuteAsync(
            [CreateCommand(Guid.Empty)],
            ModelCommandMode.Apply);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "MODEL_WORKSPACE_INVALID");
        await sender.DidNotReceive().Send(
            Arg.Any<ICommand<ErrorOr<ModelCommandOutcome>>>(),
            Arg.Any<CancellationToken>());
    }

    private static (ModelCommandExecutor Executor, IIfsDbTransaction Transaction, ISender Sender) CreateExecutor(
        bool isOwner = true)
    {
        var dbContext = Substitute.For<IIfsDbContext>();
        var transaction = Substitute.For<IIfsDbTransaction>();
        transaction.IsOwner.Returns(isOwner);
        dbContext.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(transaction));
        var sender = Substitute.For<ISender>();
        return (new ModelCommandExecutor(dbContext, sender), transaction, sender);
    }

    private static CreateProjectCommand CreateCommand(Guid projectId) =>
        new(projectId, Guid.NewGuid(), "shop", "shop", string.Empty, "2026.10-pilot", null, [], []);
}
