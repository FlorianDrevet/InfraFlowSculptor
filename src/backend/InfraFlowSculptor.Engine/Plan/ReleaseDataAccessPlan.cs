namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseDataAccessPlan(
    string Server,
    string Database,
    ReleaseDataAccessPrincipalPlan Principal,
    string Level,
    string Exposure);
