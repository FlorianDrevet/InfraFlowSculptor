using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record InstallationChecklistItemResponse(
    string Key,
    string Text,
    InstallationChecklistItemState State,
    bool IsUserActionable,
    string? Command,
    bool IsNewSincePrevious,
    DateTimeOffset? CompletedAt);
