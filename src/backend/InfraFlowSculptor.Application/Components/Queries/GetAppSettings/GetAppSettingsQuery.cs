using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetAppSettings;

[RequiresPermission(Permission.ProjectRead)]
public sealed record GetAppSettingsQuery(Guid ProjectId, Guid ComponentId, Guid ApplicationResourceId)
    : IQuery<ErrorOr<IReadOnlyList<AppSettingSummary>>>, IProjectScopedRequest;
