using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Domain.PublicationAggregate;

public sealed record PublicationTargetInput(
    GitConnectionId GitConnectionId,
    string ProviderRepositoryId,
    string RepositoryName,
    string TargetBranch,
    string SourceBranch,
    IReadOnlyCollection<PublicationTargetDestinationInput> Destinations,
    IReadOnlyCollection<PublicationTargetFileInput> Files);
