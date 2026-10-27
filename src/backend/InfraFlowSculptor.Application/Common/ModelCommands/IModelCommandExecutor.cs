using ErrorOr;

namespace InfraFlowSculptor.Application.Common.ModelCommands;

public interface IModelCommandExecutor
{
    Task<ErrorOr<ModelCommandExecution>> ExecuteAsync(
        IReadOnlyList<IModelCommand> commands,
        ModelCommandMode mode,
        CancellationToken cancellationToken = default);
}
