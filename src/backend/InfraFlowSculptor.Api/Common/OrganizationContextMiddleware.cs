using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Api.Common;

public sealed class OrganizationContextMiddleware(RequestDelegate next)
{
    private const string OrganizationHeader = "X-Ifs-Organization";

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        HttpCurrentOrganization writableOrganization,
        IIfsDbContext dbContext)
    {
        if (!context.Request.Path.StartsWithSegments("/v1")
            || !currentUser.IsAuthenticated)
        {
            await next(context);
            return;
        }

        var agnostic = context.GetEndpoint()?.Metadata.GetMetadata<OrganizationAgnostic>() is not null;
        var header = context.Request.Headers[OrganizationHeader].FirstOrDefault();
        if (agnostic && string.IsNullOrWhiteSpace(header))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(header, out var organizationGuid) || organizationGuid == Guid.Empty)
        {
            if (agnostic)
            {
                await next(context);
            }
            else
            {
                await NotFoundAsync(context);
            }

            return;
        }

        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(context.RequestAborted);
        var organizationId = new OrganizationId(organizationGuid);
        var isMember = profileId.Value != Guid.Empty
            && await dbContext.Query<OrganizationAggregate>()
                .Where(organization => organization.Id == organizationId)
                .SelectMany(organization => organization.Members)
                .AnyAsync(member => member.UserProfileId == profileId, context.RequestAborted);

        if (!isMember)
        {
            if (agnostic)
            {
                await next(context);
            }
            else
            {
                await NotFoundAsync(context);
            }

            return;
        }

        writableOrganization.Set(organizationId);
        await next(context);
    }

    private static Task NotFoundAsync(HttpContext context) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "The requested resource was not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND" })
        .ExecuteAsync(context);
}
