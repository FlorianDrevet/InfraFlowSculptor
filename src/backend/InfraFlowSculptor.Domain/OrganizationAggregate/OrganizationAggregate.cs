using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.UserProfileAggregate;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

public sealed class OrganizationAggregate : AggregateRoot<OrganizationId>, IHasVersion
{
    private readonly List<OrganizationMember> members = [];
    private string[] allowedEmailDomains = [];
    private Guid[] allowedTenantIds = [];

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<OrganizationMember> Members => members.AsReadOnly();

    public IReadOnlyCollection<string> AllowedEmailDomains => Array.AsReadOnly(allowedEmailDomains);

    public IReadOnlyCollection<Guid> AllowedTenantIds => Array.AsReadOnly(allowedTenantIds);

    public int Version { get; private set; } = 1;

    private OrganizationAggregate()
    {
    }

    private OrganizationAggregate(OrganizationId id, string name) : base(id)
    {
        Name = name;
    }

    public static ErrorOr<OrganizationAggregate> Create(
        OrganizationId id,
        string name,
        UserProfileId creatorProfileId)
    {
        var normalizedName = NormalizeAndValidateName(name);
        if (normalizedName.IsError)
        {
            return normalizedName.Errors;
        }

        if (id.Value == Guid.Empty || creatorProfileId.Value == Guid.Empty)
        {
            return Error.Validation("ORG_IDENTITY_REQUIRED", "An organization and creator identifier are required.");
        }

        var organization = new OrganizationAggregate(id, normalizedName.Value);
        organization.members.Add(new OrganizationMember(creatorProfileId, [OrganizationRole.Administrator]));
        return organization;
    }

    public ErrorOr<Success> Rename(string name)
    {
        var normalizedName = NormalizeAndValidateName(name);
        if (normalizedName.IsError)
        {
            return normalizedName.Errors;
        }

        Name = normalizedName.Value;
        return Result.Success;
    }

    public ErrorOr<Success> AddMember(UserProfileId profileId, IEnumerable<OrganizationRole> roles)
    {
        if (profileId.Value == Guid.Empty)
        {
            return Error.Validation("ORG_MEMBER_PROFILE_REQUIRED", "An organization member profile is required.");
        }

        if (members.Any(member => member.UserProfileId == profileId))
        {
            return Error.Conflict("RG-ORG-04", "A user can only be a member once in an organization.");
        }

        var memberRoles = NormalizeRoles(roles);
        if (memberRoles.IsError)
        {
            return memberRoles.Errors;
        }

        members.Add(new OrganizationMember(profileId, memberRoles.Value));
        return Result.Success;
    }

    public ErrorOr<Success> ChangeMemberRoles(UserProfileId profileId, IEnumerable<OrganizationRole> roles)
    {
        var member = members.SingleOrDefault(candidate => candidate.UserProfileId == profileId);
        if (member is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var normalizedRoles = NormalizeRoles(roles);
        if (normalizedRoles.IsError)
        {
            return normalizedRoles.Errors;
        }

        if (member.Roles.Contains(OrganizationRole.Administrator)
            && !normalizedRoles.Value.Contains(OrganizationRole.Administrator)
            && CountAdministrators() == 1)
        {
            return LastAdministratorError();
        }

        member.ChangeRoles(normalizedRoles.Value);
        return Result.Success;
    }

    public ErrorOr<Success> RemoveMember(UserProfileId profileId)
    {
        var member = members.SingleOrDefault(candidate => candidate.UserProfileId == profileId);
        if (member is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        if (member.Roles.Contains(OrganizationRole.Administrator) && CountAdministrators() == 1)
        {
            return LastAdministratorError();
        }

        members.Remove(member);
        return Result.Success;
    }

    public ErrorOr<Success> LeaveOrganization(UserProfileId profileId) => RemoveMember(profileId);

    public ErrorOr<Success> UpdateAccessRestrictions(
        IEnumerable<string> emailDomains,
        IEnumerable<Guid> tenantIds)
    {
        var normalizedDomains = NormalizeEmailDomains(emailDomains);
        if (normalizedDomains.IsError)
        {
            return normalizedDomains.Errors;
        }

        var normalizedTenantIds = NormalizeTenantIds(tenantIds);
        if (normalizedTenantIds.IsError)
        {
            return normalizedTenantIds.Errors;
        }

        allowedEmailDomains = normalizedDomains.Value;
        allowedTenantIds = normalizedTenantIds.Value;
        return Result.Success;
    }

    public bool IsOutsideAccessRestrictions(UserProfile profile)
    {
        if (allowedTenantIds.Length > 0 && !allowedTenantIds.Contains(profile.TenantId))
        {
            return true;
        }

        if (allowedEmailDomains.Length == 0)
        {
            return false;
        }

        var email = profile.Email;
        var separatorIndex = email?.LastIndexOf('@') ?? -1;
        return separatorIndex < 0
            || !allowedEmailDomains.Contains(
                email![(separatorIndex + 1)..],
                StringComparer.OrdinalIgnoreCase);
    }

    private int CountAdministrators() =>
        members.Count(member => member.Roles.Contains(OrganizationRole.Administrator));

    private static Error LastAdministratorError() =>
        Error.Conflict("RG-ORG-05", "The last organization administrator cannot leave, be removed, or be demoted.");

    private static ErrorOr<OrganizationRole[]> NormalizeRoles(IEnumerable<OrganizationRole> roles)
    {
        var normalizedRoles = roles?.Distinct().ToArray() ?? [];
        if (normalizedRoles.Length == 0)
        {
            return Error.Validation("ORG_MEMBER_ROLE_REQUIRED", "At least one organization role is required.");
        }

        if (normalizedRoles.Any(role => !Enum.IsDefined(role)))
        {
            return Error.Validation("ORG_MEMBER_ROLE_INVALID", "The organization role is not supported.");
        }

        return normalizedRoles;
    }

    private static ErrorOr<string[]> NormalizeEmailDomains(IEnumerable<string> domains)
    {
        var values = domains?.ToArray() ?? [];
        if (values.Any(domain => string.IsNullOrWhiteSpace(domain)
            || domain.Contains('@', StringComparison.Ordinal)
            || Uri.CheckHostName(domain.Trim()) != UriHostNameType.Dns))
        {
            return Error.Validation(
                "ORG_EMAIL_DOMAIN_INVALID",
                "Allowed email domains must be valid domain names without an @ prefix.");
        }

        return values
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static ErrorOr<Guid[]> NormalizeTenantIds(IEnumerable<Guid> tenantIds)
    {
        var values = tenantIds?.ToArray() ?? [];
        if (values.Any(tenantId => tenantId == Guid.Empty))
        {
            return Error.Validation("ORG_TENANT_ID_INVALID", "Allowed Entra tenant identifiers cannot be empty.");
        }

        return values.Distinct().Order().ToArray();
    }

    private static ErrorOr<string> NormalizeAndValidateName(string name)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 2 or > 80)
        {
            return Error.Validation("ORG_NAME_INVALID", "Organization name must contain between 2 and 80 characters.");
        }

        return normalizedName;
    }
}
