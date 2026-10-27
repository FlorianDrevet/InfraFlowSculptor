using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Application.Common.Auditing;

public sealed record AuditEntityChange(
    ObjectType ObjectType,
    Guid ObjectId,
    Guid OrganizationId,
    IReadOnlyList<AuditPropertyChange> Properties);
