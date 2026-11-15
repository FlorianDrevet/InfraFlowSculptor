using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Projects.Installation;

public sealed record InstallationChecklistItemView(
    string Key,
    string Text,
    InstallationChecklistItemState State,
    bool IsUserActionable,
    string? Command,
    bool IsNewSincePrevious,
    DateTimeOffset? CompletedAt);
