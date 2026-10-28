using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Queries.ListProjectComponents;

public sealed class ListProjectComponentsQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<ListProjectComponentsQuery, ErrorOr<IReadOnlyList<ComponentSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<ComponentSummary>>> Handle(
        ListProjectComponentsQuery query,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var components = await dbContext.Query<ComponentAggregate>()
            .AsNoTracking()
            .Where(component => component.ProjectId == projectId)
            .OrderBy(component => component.NameKey)
            .ToArrayAsync(cancellationToken);
        return components.Select(ComponentSummaryMapper.Map).ToArray();
    }
}
