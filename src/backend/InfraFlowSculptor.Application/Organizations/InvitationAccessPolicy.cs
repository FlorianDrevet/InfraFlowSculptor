using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations;

public static class InvitationAccessPolicy
{
    [SuppressMessage(
        "Globalization",
        "CA1862",
        Justification = "EF Core translates ToLower to SQL; OrdinalIgnoreCase string.Equals is not provider-translatable.")]
    public static async Task<ErrorOr<Success>> ValidateAsync(
        IIfsDbContext dbContext,
        OrganizationAggregate organization,
        string email,
        CancellationToken cancellationToken)
    {
        if (!organization.IsInvitationEmailAllowed(email))
        {
            return AccessRestrictionError();
        }

        if (organization.AllowedTenantIds.Count == 0)
        {
            return Result.Success;
        }

        var recipientTenantIds = await dbContext.Query<UserProfile>()
            .Where(profile => profile.Email != null && profile.Email.ToLower() == email)
            .Select(profile => profile.TenantId)
            .ToListAsync(cancellationToken);
        return recipientTenantIds.Any(organization.IsInvitationTenantAllowed)
            ? Result.Success
            : AccessRestrictionError();
    }

    private static Error AccessRestrictionError() =>
        Error.Forbidden("RG-ORG-06", "The invited address does not meet the organization's domain or tenant restrictions.");
}
