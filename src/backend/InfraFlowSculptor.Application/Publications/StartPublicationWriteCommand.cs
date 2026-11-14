using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Publications;

[RequiresPermission(Permission.Publish)]
[Audited(AuditActions.PublicationWriteRequested, ObjectType.Publication)]
public sealed record StartPublicationWriteCommand(Guid ProjectId, Guid PublicationId)
    : ICommand<ErrorOr<PublicationView>>, IProjectScopedRequest;
