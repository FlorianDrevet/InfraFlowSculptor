namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed record CreateProjectFromWizardDestination(
    Guid GitConnectionId,
    string ProviderRepositoryId,
    string BasePath);
