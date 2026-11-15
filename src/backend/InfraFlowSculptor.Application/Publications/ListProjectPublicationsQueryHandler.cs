using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.PublicationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public sealed class ListProjectPublicationsQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<ListProjectPublicationsQuery, ErrorOr<IReadOnlyCollection<PublicationView>>>
{
    public async ValueTask<ErrorOr<IReadOnlyCollection<PublicationView>>> Handle(
        ListProjectPublicationsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Limit is < 1 or > 100)
        {
            return Error.Validation("PUBLICATION_LIMIT_INVALID", "La limite doit être comprise entre 1 et 100.");
        }

        var publications = await dbContext.Query<Publication>()
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == new ProjectId(query.ProjectId))
            .OrderByDescending(candidate => candidate.RequestedAt)
            .Take(query.Limit)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Destinations)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Files)
            .ToArrayAsync(cancellationToken);
        return publications.Select(PublicationViewMapper.Map).ToArray();
    }
}
