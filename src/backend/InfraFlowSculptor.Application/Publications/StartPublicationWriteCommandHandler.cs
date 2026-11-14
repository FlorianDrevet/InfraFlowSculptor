using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.PublicationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public sealed class StartPublicationWriteCommandHandler(IIfsDbContext dbContext, IJobDispatcher jobDispatcher)
    : ICommandHandler<StartPublicationWriteCommand, ErrorOr<PublicationView>>
{
    public async ValueTask<ErrorOr<PublicationView>> Handle(
        StartPublicationWriteCommand command,
        CancellationToken cancellationToken)
    {
        var publication = await dbContext.Query<Publication>()
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Destinations)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Files)
            .SingleOrDefaultAsync(candidate => candidate.Id == new PublicationId(command.PublicationId)
                && candidate.ProjectId == new ProjectId(command.ProjectId),
                cancellationToken);
        if (publication is null)
        {
            return Error.NotFound("PUBLICATION_NOT_FOUND", "La publication n'a pas été trouvée.");
        }

        var state = publication.BeginWriting();
        if (state.IsError)
        {
            return state.Errors;
        }

        jobDispatcher.Enqueue(JobQueue.Publication, publication.OrganizationId, new WritePublicationJob(command.PublicationId));
        return PublicationViewMapper.Map(publication);
    }
}
