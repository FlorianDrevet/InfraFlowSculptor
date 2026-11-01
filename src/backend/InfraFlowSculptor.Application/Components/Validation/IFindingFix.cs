using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Application.Components.Validation;

public interface IFindingFix : IKeyed<string>
{
    ErrorOr<IModelCommand> CreateCommand(FindingFixContext context);
}
