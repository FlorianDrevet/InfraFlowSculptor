using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.OrganizationAggregate;

public sealed class OrganizationAggregate : AggregateRoot<OrganizationId>, IHasVersion
{
    private readonly List<OrganizationMember> members = [];

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<OrganizationMember> Members => members.AsReadOnly();

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

        var memberRoles = roles.Distinct().ToArray();
        if (memberRoles.Length == 0)
        {
            return Error.Validation("ORG_MEMBER_ROLE_REQUIRED", "At least one organization role is required.");
        }

        members.Add(new OrganizationMember(profileId, memberRoles));
        return Result.Success;
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
