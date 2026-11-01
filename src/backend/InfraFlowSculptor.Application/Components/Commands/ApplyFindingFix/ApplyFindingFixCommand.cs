using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.ApplyFindingFix;

[RequiresPermission(Permission.ModelEdit)]
public sealed record ApplyFindingFixCommand(
    Guid ProjectId,
    string FindingCode,
    ObjectType SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId,
    int ExpectedObjectVersion,
    string? DetailKey = null) : ICommand<ErrorOr<ModelCommandExecution>>, IProjectScopedRequest
{
    public string? NormalizedDetailKey => DetailKey?.Trim();
}
