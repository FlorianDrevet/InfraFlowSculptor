using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Api.Common;

public sealed class UserProfileSyncMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        IIfsDbContext dbContext,
        TimeProvider clock)
    {
        if (currentUser.IsAuthenticated && !currentUser.Key.IsEmpty)
        {
            var now = clock.GetUtcNow();
            var profile = await dbContext.Query<UserProfile>()
                .SingleOrDefaultAsync(
                    candidate => candidate.TenantId == currentUser.Key.TenantId
                        && candidate.ObjectId == currentUser.Key.ObjectId,
                    context.RequestAborted);

            if (profile is null)
            {
                var created = UserProfile.Create(
                    UserProfileId.New(clock),
                    currentUser.Key,
                    currentUser.DisplayName,
                    currentUser.VerifiedEmail?.Value,
                    now);
                if (!created.IsError)
                {
                    dbContext.Query<UserProfile>().Add(created.Value);
                    try
                    {
                        await dbContext.SaveChangesAsync(context.RequestAborted);
                    }
                    catch (DbUpdateException exception) when (IsConcurrentProfileCreation(exception))
                    {
                        dbContext.Query<UserProfile>().Remove(created.Value);
                    }
                }
            }
            else if (now - profile.LastSeenAt > SyncInterval)
            {
                profile.SynchronizeIdentity(currentUser.DisplayName, currentUser.VerifiedEmail?.Value, now);
                await dbContext.SaveChangesAsync(context.RequestAborted);
            }
        }

        await next(context);
    }

    private static bool IsConcurrentProfileCreation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_user_profiles_tenant_id_object_id"
        };
}
