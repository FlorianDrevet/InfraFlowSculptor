namespace InfraFlowSculptor.Application.Generation;

public sealed record RevisionListView(IReadOnlyList<RevisionView> Items, int NextNumber);
