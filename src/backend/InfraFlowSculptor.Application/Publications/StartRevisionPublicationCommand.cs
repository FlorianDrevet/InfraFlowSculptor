using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Publications;

[RequiresPermission(Permission.Publish)]
[Audited(AuditActions.PublicationRequested, ObjectType.Publication)]
public sealed record StartRevisionPublicationCommand(
    Guid ProjectId,
    Guid RevisionId,
    string? Message,
    bool AcceptStaleRevision,
    IReadOnlyCollection<Guid>? DestinationIds)
    : ICommand<ErrorOr<PublicationView>>, IProjectScopedRequest;
