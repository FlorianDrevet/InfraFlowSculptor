using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record InstallationChecklistItem(
    string Key,
    string Text,
    InstallationChecklistItemState State,
    bool IsUserActionable,
    string? Command = null,
    bool IsNewSincePrevious = false);
