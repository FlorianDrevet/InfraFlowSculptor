namespace InfraFlowSculptor.Contracts.Projects;

public sealed record UpdateProjectSettingsRequest(
    string Name,
    string Code,
    string Description,
    bool SystemTagsEnabled,
    Guid? CoordinatorUserProfileId,
    IReadOnlyDictionary<string, string> Tags,
    bool ConfirmImpact,
    int ExpectedVersion);
