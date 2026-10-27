using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Common.ModelCommands;

public interface IModelCommand : ICommand<ErrorOr<ModelCommandOutcome>>
{
    ModelWorkspaceId Workspace { get; }
}
