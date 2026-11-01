namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkMutationResponse(Guid LinkId, string Summary, IReadOnlyList<string> Effects, IReadOnlyList<string> Findings);
