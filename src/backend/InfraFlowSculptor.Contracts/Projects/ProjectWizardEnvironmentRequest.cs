namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectWizardEnvironmentRequest(
    string Name,
    string Code,
    Guid SubscriptionId,
    string Region,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    string Description = "");
