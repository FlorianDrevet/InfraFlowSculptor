using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditEventItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActorDisplayName,
    AuditVia Via,
    string Action,
    ObjectType ObjectType,
    Guid ObjectId,
    Guid? ProjectId,
    AuditEventDiff Diff);
