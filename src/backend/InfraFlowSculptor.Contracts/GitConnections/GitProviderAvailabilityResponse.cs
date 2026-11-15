using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record GitProviderAvailabilityResponse(GitProviderKind Kind, string DisplayName);
