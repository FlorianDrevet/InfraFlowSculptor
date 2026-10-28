using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActorDisplayName,
    string Via,
    string Action,
    ObjectType ObjectType,
    Guid ObjectId,
    Guid? ProjectId,
    AuditEventDiffResponse Diff);
