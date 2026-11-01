namespace InfraFlowSculptor.Engine.Plan;

public sealed record TargetPlan(
    TargetInfo Target,
    IReadOnlyList<ResourceGroupPlan> ResourceGroups,
    IReadOnlyList<ResourcePlan> Resources,
    IReadOnlyList<ExternalReference> ExternalReferences,
    IReadOnlyList<RoleAssignmentPlan> RoleAssignments,
    IReadOnlyList<AppSettingPlan> AppSettings,
    IReadOnlyList<PipelineSecretPlan> ExpectedSecrets,
    IReadOnlyList<DataAccessPlan> DataAccess,
    IReadOnlyList<RemovedResourcePlan> Removed,
    ReleaseDataPlan Release);
