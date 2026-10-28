using InfraFlowSculptor.Application.Common.Auditing;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class EfAuditEventPurger(IfsDbContext dbContext) : IAuditEventPurger
{
    public Task<long> PurgeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<long>(
                $"SELECT public.purge_audit_events({cutoff}) AS \"Value\"")
            .SingleAsync(cancellationToken);
}
