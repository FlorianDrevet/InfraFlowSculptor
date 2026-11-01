namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseSecretWritePlan(
    string Variable,
    string Vault,
    string Secret,
    string Source);
