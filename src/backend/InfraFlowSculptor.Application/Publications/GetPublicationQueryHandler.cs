using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.PublicationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public sealed class GetPublicationQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetPublicationQuery, ErrorOr<PublicationView>>
{
    public async ValueTask<ErrorOr<PublicationView>> Handle(GetPublicationQuery query, CancellationToken cancellationToken)
    {
        var publication = await dbContext.Query<Publication>()
            .AsNoTracking()
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Destinations)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Files)
            .SingleOrDefaultAsync(candidate => candidate.Id == new PublicationId(query.PublicationId)
                && candidate.ProjectId == new ProjectId(query.ProjectId),
                cancellationToken);
        return publication is null
            ? Error.NotFound("PUBLICATION_NOT_FOUND", "La publication n'a pas été trouvée.")
            : PublicationViewMapper.Map(publication);
    }
}
