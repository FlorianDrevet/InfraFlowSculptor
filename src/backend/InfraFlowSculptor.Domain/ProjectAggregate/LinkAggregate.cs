using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

[AuditedObject(ObjectType.Link, nameof(Id), nameof(OrganizationId))]
public sealed class LinkAggregate : AggregateRoot<Guid>, IOrganizationOwned, IHasVersion
{
    private static readonly HashSet<LinkKind> DataAccessKinds = [LinkKind.DataAccess];
    private readonly List<LinkRole> roles = [];

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public Guid SourceComponentId { get; private set; }

    public Guid SourceResourceId { get; private set; }

    public Guid TargetComponentId { get; private set; }

    public Guid TargetResourceId { get; private set; }

    public LinkKind Kind { get; private set; }

    public LinkOrigin Origin { get; private set; }

    public LinkIdentityKind IdentityKind { get; private set; }

    public Guid? IdentityResourceId { get; private set; }

    public IReadOnlyCollection<LinkRole> Roles => roles.AsReadOnly();

    public LinkAccessScope AccessScope { get; private set; }

    public DataAccessLevel? DataAccessLevel { get; private set; }

    public int Version { get; private set; } = 1;

    private LinkAggregate()
    {
    }

    public static ErrorOr<LinkAggregate> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind,
        Guid? identityResourceId,
        IEnumerable<string>? roles,
        LinkAccessScope accessScope,
        DataAccessLevel? dataAccessLevel)
    {
        var valid = Validate(
            id,
            projectId,
            organizationId,
            sourceComponentId,
            sourceResourceId,
            targetComponentId,
            targetResourceId,
            kind,
            identityKind,
            identityResourceId,
            roles,
            accessScope,
            dataAccessLevel);
        if (valid.IsError)
        {
            return valid.Errors;
        }

        var link = new LinkAggregate
        {
            Id = id,
            ProjectId = projectId,
            OrganizationId = organizationId,
            SourceComponentId = sourceComponentId,
            SourceResourceId = sourceResourceId,
            TargetComponentId = targetComponentId,
            TargetResourceId = targetResourceId,
            Kind = kind,
            Origin = LinkOrigin.Explicit,
            IdentityKind = identityKind,
            IdentityResourceId = identityResourceId,
            AccessScope = accessScope,
            DataAccessLevel = dataAccessLevel
        };
        link.roles.AddRange(NormalizeRoles(roles));
        return link;
    }

    public ErrorOr<Success> Update(
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind,
        Guid? identityResourceId,
        IEnumerable<string>? desiredRoles,
        LinkAccessScope accessScope,
        DataAccessLevel? dataAccessLevel)
    {
        var valid = Validate(
            Id,
            ProjectId,
            OrganizationId,
            sourceComponentId,
            sourceResourceId,
            targetComponentId,
            targetResourceId,
            kind,
            identityKind,
            identityResourceId,
            desiredRoles,
            accessScope,
            dataAccessLevel);
        if (valid.IsError)
        {
            return valid.Errors;
        }

        SourceComponentId = sourceComponentId;
        SourceResourceId = sourceResourceId;
        TargetComponentId = targetComponentId;
        TargetResourceId = targetResourceId;
        Kind = kind;
        IdentityKind = identityKind;
        IdentityResourceId = identityResourceId;
        AccessScope = accessScope;
        DataAccessLevel = dataAccessLevel;
        roles.Clear();
        roles.AddRange(NormalizeRoles(desiredRoles));
        Touch();
        return Result.Success;
    }

    private static ErrorOr<Success> Validate(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid sourceComponentId,
        Guid sourceResourceId,
        Guid targetComponentId,
        Guid targetResourceId,
        LinkKind kind,
        LinkIdentityKind identityKind,
        Guid? identityResourceId,
        IEnumerable<string>? roleNames,
        LinkAccessScope accessScope,
        DataAccessLevel? dataAccessLevel)
    {
        if (id == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty
            || sourceComponentId == Guid.Empty || sourceResourceId == Guid.Empty
            || targetComponentId == Guid.Empty || targetResourceId == Guid.Empty)
        {
            return Error.Validation("LINK_FIELDS_REQUIRED", "A link, project, source, target, and organization are required.");
        }

        if (sourceResourceId == targetResourceId)
        {
            return Error.Validation("RG-LIA-02", "A resource cannot be linked to itself.");
        }

        if (!Enum.IsDefined(kind) || !Enum.IsDefined(identityKind) || !Enum.IsDefined(accessScope)
            || (dataAccessLevel is not null && !Enum.IsDefined(dataAccessLevel.Value)))
        {
            return Error.Validation("LINK_PARAMETER_INVALID", "A link contains an unsupported parameter value.");
        }

        if (dataAccessLevel is not null && !DataAccessKinds.Contains(kind))
        {
            return Error.Validation("LINK_DATA_ACCESS_LEVEL_UNSUPPORTED", "A data access level is only valid for a data access link.");
        }

        if ((identityKind == LinkIdentityKind.UserAssigned) != (identityResourceId is not null)
            || identityResourceId == Guid.Empty)
        {
            return Error.Validation("RG-LIA-08-IDENTITY-REQUIRED", "A user-assigned identity selection must name one project identity.");
        }

        var normalizedRoles = NormalizeRoles(roleNames);
        if (normalizedRoles.Any(role => string.IsNullOrWhiteSpace(role.Name)))
        {
            return Error.Validation("RG-LIA-13-ROLE-REQUIRED", "Role names cannot be empty.");
        }

        if (normalizedRoles.Select(role => role.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedRoles.Length)
        {
            return Error.Validation("LINK_ROLE_DUPLICATE", "A role can only be selected once on a link.");
        }

        return Result.Success;
    }

    private static LinkRole[] NormalizeRoles(IEnumerable<string>? roleNames) => (roleNames ?? [])
        .Select(name => new LinkRole(name?.Trim() ?? string.Empty))
        .OrderBy(role => role.Name, StringComparer.Ordinal)
        .ToArray();

    private void Touch() => Version = checked(Version + 1);
}
