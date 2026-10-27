using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class AuditEventEntity : IOrganizationOwned
{
    public Guid Id { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string ActorKind { get; private set; } = string.Empty;

    public string ActorRef { get; private set; } = string.Empty;

    public AuditVia Via { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public ObjectType ObjectType { get; private set; }

    public Guid ObjectId { get; private set; }

    public string Diff { get; private set; } = string.Empty;

    private AuditEventEntity()
    {
    }

    public AuditEventEntity(
        Guid id,
        OrganizationId organizationId,
        DateTimeOffset occurredAt,
        string actorKind,
        string actorRef,
        AuditVia via,
        string action,
        ObjectType objectType,
        Guid objectId,
        string diff,
        Guid? projectId = null)
    {
        Id = id;
        OrganizationId = organizationId;
        OccurredAt = occurredAt;
        ActorKind = actorKind;
        ActorRef = actorRef;
        Via = via;
        Action = action;
        ObjectType = objectType;
        ObjectId = objectId;
        ProjectId = projectId;
        Diff = diff;
    }
}
