using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

public sealed class GetResourceDeletionImpactQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : IQueryHandler<GetResourceDeletionImpactQuery, ErrorOr<ResourceDeletionImpactSummary>>
{
    public async ValueTask<ErrorOr<ResourceDeletionImpactSummary>> Handle(
        GetResourceDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext,
            currentOrganization,
            query.ProjectId,
            query.ComponentId,
            cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var resource = loaded.Value.Component.Resources.SingleOrDefault(candidate => candidate.Id == query.ResourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        return new ResourceDeletionImpactSummary(
            resource.Version,
            resource.Children
                .OrderBy(child => child.Type, StringComparer.Ordinal)
                .ThenBy(child => child.Name, StringComparer.Ordinal)
                .Select(child => new ResourceDeletionImpactItemSummary(child.Type, child.Name))
                .ToArray());
    }
}
