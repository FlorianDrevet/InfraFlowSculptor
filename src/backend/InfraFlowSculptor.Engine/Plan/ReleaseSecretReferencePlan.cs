namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseSecretReferencePlan(
    string Vault,
    string Secret,
    string Writer);
