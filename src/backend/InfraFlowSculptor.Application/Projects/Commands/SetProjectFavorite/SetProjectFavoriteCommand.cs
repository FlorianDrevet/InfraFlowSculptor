using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectFavorite;

[RequiresPermission(Permission.ProjectRead)]
public sealed record SetProjectFavoriteCommand(Guid ProjectId, bool IsFavorite)
    : ICommand<ErrorOr<Success>>, IProjectScopedRequest;
