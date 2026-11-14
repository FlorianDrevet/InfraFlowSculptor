using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Generation;

[RequiresPermission(Permission.ProjectAdminister)]
[Audited(AuditActions.ProjectExported, ObjectType.Project, IncludeUnchanged = true)]
public sealed record ExportProjectCommand(Guid ProjectId)
    : ICommand<ErrorOr<ProjectExportLink>>, IProjectScopedRequest;
