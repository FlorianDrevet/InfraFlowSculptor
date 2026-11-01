namespace InfraFlowSculptor.Engine.Plan;

public sealed record RoleAssignmentPlan(
    string PrincipalKind,
    string Principal,
    string Role,
    string ScopeKind,
    string Scope,
    string Name,
    IReadOnlyList<RoleAssignmentOriginPlan> Origins);
