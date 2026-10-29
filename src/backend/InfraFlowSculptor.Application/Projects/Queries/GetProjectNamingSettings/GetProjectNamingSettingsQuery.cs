using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.GetProjectNamingSettings;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetProjectNamingSettingsQuery(Guid ProjectId)
    : IQuery<ErrorOr<ProjectNamingSettings>>, IProjectScopedRequest;
