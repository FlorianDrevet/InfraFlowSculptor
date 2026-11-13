namespace InfraFlowSculptor.Domain.RevisionAggregate;

public sealed record RevisionChangeSummary(IReadOnlyList<RevisionChange> Changes)
{
    public static RevisionChangeSummary Empty { get; } = new([]);
}
