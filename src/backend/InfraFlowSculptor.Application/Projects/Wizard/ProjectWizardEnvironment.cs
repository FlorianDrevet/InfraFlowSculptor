namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed record ProjectWizardEnvironment(
    Guid Id,
    string Name,
    string Code,
    Guid SubscriptionId,
    string Region,
    bool IsProtected,
    IReadOnlyList<string> Approvers,
    string Description);
