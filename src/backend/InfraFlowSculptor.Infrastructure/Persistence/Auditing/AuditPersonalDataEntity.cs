using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class AuditPersonalDataEntity
{
    public Guid EventId { get; private set; }

    public UserProfileId? SubjectUserId { get; private set; }

    public string Field { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public AuditEventEntity AuditEvent { get; private set; } = null!;

    private AuditPersonalDataEntity()
    {
    }

    public AuditPersonalDataEntity(Guid eventId, UserProfileId? subjectUserId, string field, string value)
    {
        EventId = eventId;
        SubjectUserId = subjectUserId;
        Field = field;
        Value = value;
    }

    public void AssignSubject(UserProfileId subjectUserId) => SubjectUserId = subjectUserId;
}
