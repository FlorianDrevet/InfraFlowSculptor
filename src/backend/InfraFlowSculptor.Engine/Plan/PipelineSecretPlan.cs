namespace InfraFlowSculptor.Engine.Plan;

public sealed record PipelineSecretPlan(
    string Variable,
    string Vault,
    string Secret,
    string Writer);
