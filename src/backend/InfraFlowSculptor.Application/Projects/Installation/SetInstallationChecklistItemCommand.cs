using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Installation;

[RequiresPermission(Permission.InstallationManage)]
[Audited(AuditActions.InstallationChecklistItemUpdated, ObjectType.InstallationChecklistItem)]
public sealed record SetInstallationChecklistItemCommand(
    Guid ProjectId,
    string ItemKey,
    bool IsCompleted) : ICommand<ErrorOr<Success>>, IProjectScopedRequest;
