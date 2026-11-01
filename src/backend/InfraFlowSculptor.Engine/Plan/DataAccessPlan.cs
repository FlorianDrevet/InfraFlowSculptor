namespace InfraFlowSculptor.Engine.Plan;

public sealed record DataAccessPlan(
    string Server,
    string Database,
    string PrincipalKind,
    string PrincipalResourceId,
    string Level,
    IReadOnlyList<string> DatabaseRoles,
    string Exposure,
    string ApplyScript,
    string RemoveScript);
