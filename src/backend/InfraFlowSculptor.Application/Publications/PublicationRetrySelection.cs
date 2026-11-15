using InfraFlowSculptor.Domain.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public static class PublicationRetrySelection
{
    public static bool IsValid(PublicationAggregate previousPublication, IReadOnlyCollection<Guid> destinationIds)
    {
        ArgumentNullException.ThrowIfNull(previousPublication);
        ArgumentNullException.ThrowIfNull(destinationIds);

        if (destinationIds.Count == 0
            || destinationIds.Distinct().Count() != destinationIds.Count
            || previousPublication.State is not (PublicationState.Failed or PublicationState.PartiallyPublished))
        {
            return false;
        }

        var selectedIds = destinationIds.ToHashSet();
        return previousPublication.Targets
            .Where(target => target.State == PublicationTargetState.Failed)
            .Any(target => target.Destinations
                .Select(destination => destination.DestinationId)
                .ToHashSet()
                .SetEquals(selectedIds));
    }
}
