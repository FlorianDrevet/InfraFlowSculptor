namespace InfraFlowSculptor.Contracts.Projects;

public sealed record CreateProjectFromWizardDestinationRequest(
    Guid GitConnectionId,
    string ProviderRepositoryId,
    string BasePath);
