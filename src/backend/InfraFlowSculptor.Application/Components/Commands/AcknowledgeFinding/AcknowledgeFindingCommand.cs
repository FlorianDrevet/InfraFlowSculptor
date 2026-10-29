using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.AcknowledgeFinding;

[RequiresPermission(Permission.ModelEdit)]
[Audited(AuditActions.FindingAcknowledged, ObjectType.FindingAcknowledgment)]
public sealed record AcknowledgeFindingCommand(
    Guid ProjectId,
    string FindingCode,
    ObjectType SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId,
    int ExpectedObjectVersion,
    string Comment,
    string? DetailKey = null) : ICommand<ErrorOr<Success>>, IProjectScopedRequest
{
    public string? NormalizedDetailKey => DetailKey?.Trim();
}
