using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.GetEffectivePermissions;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetEffectivePermissionsQuery(Guid ProjectId, Guid? MemberId)
    : IQuery<ErrorOr<ProjectEffectivePermissions>>, IProjectScopedRequest;
