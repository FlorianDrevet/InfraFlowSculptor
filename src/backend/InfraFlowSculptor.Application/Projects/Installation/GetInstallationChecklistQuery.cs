using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Installation;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetInstallationChecklistQuery(Guid ProjectId)
    : IQuery<ErrorOr<InstallationChecklistView>>, IProjectScopedRequest;
