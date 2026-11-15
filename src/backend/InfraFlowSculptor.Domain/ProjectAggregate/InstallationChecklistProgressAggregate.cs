using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

[AuditedObject(ObjectType.InstallationChecklistItem, nameof(Id), nameof(OrganizationId))]
public sealed class InstallationChecklistProgressAggregate : AggregateRoot<Guid>, IOrganizationOwned
{
    private InstallationChecklistProgressAggregate()
    {
    }

    private InstallationChecklistProgressAggregate(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string itemKey,
        UserProfileId completedBy,
        DateTimeOffset completedAt) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        ItemKey = itemKey;
        CompletedBy = completedBy;
        CompletedAt = completedAt;
    }

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string ItemKey { get; private set; } = string.Empty;

    public UserProfileId? CompletedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static InstallationChecklistProgressAggregate Complete(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string itemKey,
        UserProfileId completedBy,
        DateTimeOffset completedAt) =>
        new(id, projectId, organizationId, itemKey, completedBy, completedAt);

    public void SetCompleted(bool isCompleted, UserProfileId completedBy, DateTimeOffset changedAt)
    {
        CompletedBy = isCompleted ? completedBy : null;
        CompletedAt = isCompleted ? changedAt : null;
    }
}
