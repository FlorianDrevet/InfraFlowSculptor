using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Publications;

[RequiresPermission(Permission.Publish)]
[Audited(AuditActions.PublicationOwnershipConfirmed, ObjectType.Publication)]
public sealed record ConfirmPublicationOwnershipCommand(Guid ProjectId, Guid PublicationId, Guid TargetId)
    : ICommand<ErrorOr<PublicationView>>, IProjectScopedRequest;
