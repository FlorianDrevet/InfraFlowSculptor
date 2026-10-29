namespace InfraFlowSculptor.Engine.Links;

public sealed record WiringResult(
    IReadOnlyList<WiredLink> Links,
    IReadOnlyList<WiredIdentity> Identities,
    IReadOnlyList<RoleAssignmentSnapshot> RoleAssignments,
    IReadOnlyList<WiringRoleEquivalence> RoleEquivalences,
    IReadOnlyList<WiringDependency> Dependencies,
    IReadOnlyList<DataAccessRequest> DataAccessRequests,
    IReadOnlyList<WiringIssue> Issues);
