using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class ListGitConnectionsQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : IQueryHandler<ListGitConnectionsQuery, ErrorOr<IReadOnlyCollection<GitConnectionSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyCollection<GitConnectionSummary>>> Handle(
        ListGitConnectionsQuery query,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(query.OrganizationId);
        if (organizationId.Value == Guid.Empty || currentOrganization.Id != organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var connections = await dbContext.Query<GitConnection>()
            .AsNoTracking()
            .Include(connection => connection.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .OrderBy(connection => connection.Name)
            .ToArrayAsync(cancellationToken);
        var usageRows = await (
                from usage in dbContext.Query<GitConnectionPublicationUse>()
                join project in dbContext.Query<ProjectAggregate>() on usage.ProjectId equals project.Id
                select new { usage.ConnectionId, project.Name })
            .ToArrayAsync(cancellationToken);

        return connections.Select(connection => GitConnectionSummaryMapper.Map(
            connection,
            usageRows
                .Where(usage => usage.ConnectionId == connection.Id)
                .Select(usage => usage.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray())).ToArray();
    }
}
