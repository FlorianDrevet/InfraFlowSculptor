namespace InfraFlowSculptor.Engine.Plan;

public sealed record InstallationChecklist(IReadOnlyList<InstallationChecklistSection> Sections);
