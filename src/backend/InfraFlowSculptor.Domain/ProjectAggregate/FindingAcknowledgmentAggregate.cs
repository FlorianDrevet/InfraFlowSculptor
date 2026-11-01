using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

[AuditedObject(ObjectType.FindingAcknowledgment, nameof(Id), nameof(OrganizationId))]
public sealed partial class FindingAcknowledgmentAggregate : AggregateRoot<Guid>, IOrganizationOwned
{
    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string FindingCode { get; private set; } = string.Empty;

    public string? DetailKey { get; private set; }

    public ObjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid? EnvironmentId { get; private set; }

    public int ObjectVersion { get; private set; }

    public string Comment { get; private set; } = string.Empty;

    public UserProfileId AcknowledgedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private FindingAcknowledgmentAggregate()
    {
    }

    private FindingAcknowledgmentAggregate(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string findingCode,
        string? detailKey,
        ObjectType subjectType,
        Guid subjectId,
        Guid? environmentId,
        int objectVersion,
        string comment,
        UserProfileId acknowledgedBy,
        DateTimeOffset createdAt) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        FindingCode = findingCode;
        DetailKey = detailKey;
        SubjectType = subjectType;
        SubjectId = subjectId;
        EnvironmentId = environmentId;
        ObjectVersion = objectVersion;
        Comment = comment;
        AcknowledgedBy = acknowledgedBy;
        CreatedAt = createdAt;
    }

    public static ErrorOr<FindingAcknowledgmentAggregate> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string findingCode,
        string? detailKey,
        ObjectType subjectType,
        Guid subjectId,
        Guid? environmentId,
        int objectVersion,
        string comment,
        UserProfileId acknowledgedBy,
        DateTimeOffset createdAt)
    {
        var normalizedCode = findingCode?.Trim() ?? string.Empty;
        var normalizedDetailKey = detailKey?.Trim();
        var normalizedComment = comment?.Trim() ?? string.Empty;
        if (id == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty
            || subjectId == Guid.Empty || acknowledgedBy.Value == Guid.Empty
            || environmentId == Guid.Empty || !Enum.IsDefined(subjectType))
        {
            return Error.Validation("FINDING_ACK_IDENTITY_REQUIRED", "A valid project, subject, and actor are required to acknowledge a finding.");
        }

        if (!FindingCodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("FINDING_ACK_CODE_INVALID", "The finding code is invalid.");
        }

        if (detailKey is not null && (string.IsNullOrWhiteSpace(normalizedDetailKey) || normalizedDetailKey.Length > 128))
        {
            return Error.Validation("FINDING_ACK_DETAIL_KEY_INVALID", "The finding detail key must contain between 1 and 128 characters.");
        }

        if (objectVersion < 1)
        {
            return Error.Validation("FINDING_ACK_VERSION_INVALID", "The acknowledged object version must be positive.");
        }

        if (normalizedComment.Length is < 1 or > 2000)
        {
            return Error.Validation("FINDING_ACK_COMMENT_REQUIRED", "An acknowledgment comment must contain between 1 and 2000 characters.");
        }

        return new FindingAcknowledgmentAggregate(
            id,
            projectId,
            organizationId,
            normalizedCode,
            normalizedDetailKey,
            subjectType,
            subjectId,
            environmentId,
            objectVersion,
            normalizedComment,
            acknowledgedBy,
            createdAt);
    }

    [GeneratedRegex("^VAL-[A-Z0-9]+(?:-[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FindingCodePattern();
}
