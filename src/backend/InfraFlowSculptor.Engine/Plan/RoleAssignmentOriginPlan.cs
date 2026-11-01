namespace InfraFlowSculptor.Engine.Plan;

public sealed record RoleAssignmentOriginPlan(
    string Kind,
    string LinkKind,
    string Source,
    string Target);
