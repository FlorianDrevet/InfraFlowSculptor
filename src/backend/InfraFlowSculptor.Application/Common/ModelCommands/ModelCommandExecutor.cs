using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using Mediator;

namespace InfraFlowSculptor.Application.Common.ModelCommands;

public sealed class ModelCommandExecutor(IIfsDbContext dbContext, ISender sender) : IModelCommandExecutor
{
    public async Task<ErrorOr<ModelCommandExecution>> ExecuteAsync(
        IReadOnlyList<IModelCommand> commands,
        ModelCommandMode mode,
        CancellationToken cancellationToken = default)
    {
        if (commands.Count == 0)
        {
            return Error.Validation("MODEL_COMMANDS_REQUIRED", "At least one model command is required.");
        }

        if (!Enum.IsDefined(mode))
        {
            return Error.Validation("MODEL_COMMAND_MODE_INVALID", "The model command mode must be Apply or DryRun.");
        }

        if (commands.Any(command => command is null
                || command.Workspace is null
                || command.Workspace.Kind != ModelWorkspaceId.MainKind
                || command.Workspace.Id == Guid.Empty)
            || commands.Select(command => command.Workspace).Distinct().Count() != 1)
        {
            return Error.Validation("MODEL_WORKSPACE_INVALID", "Jalon 0 commands must target one main project workspace.");
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var effects = new List<string>(commands.Count);
        var findings = new List<string>();
        try
        {
            foreach (var command in commands)
            {
                var result = await sender.Send(command, cancellationToken);
                if (result.IsError)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    dbContext.ClearTrackedChanges();
                    return result.Errors;
                }

                effects.Add(result.Value.Effect);
                findings.AddRange(result.Value.Findings);
            }

            if (mode == ModelCommandMode.DryRun)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new ModelCommandExecution(
                mode,
                effects,
                findings,
                $"{commands.Count} model command(s) {(mode == ModelCommandMode.Apply ? "applied" : "previewed")}.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            dbContext.ClearTrackedChanges();
            throw;
        }
    }
}
