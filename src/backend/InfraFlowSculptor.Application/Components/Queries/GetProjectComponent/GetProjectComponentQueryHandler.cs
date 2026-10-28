using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Queries.GetProjectComponent;

public sealed class GetProjectComponentQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetProjectComponentQuery, ErrorOr<ComponentSummary>>
{
    public async ValueTask<ErrorOr<ComponentSummary>> Handle(
        GetProjectComponentQuery query,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var component = await dbContext.Query<ComponentAggregate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(query.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        return component is null
            ? Error.NotFound("COMPONENT_NOT_FOUND", "The component was not found.")
            : ComponentSummaryMapper.Map(component);
    }
}
