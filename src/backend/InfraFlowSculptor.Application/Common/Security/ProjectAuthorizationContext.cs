namespace InfraFlowSculptor.Application.Common.Security;

public sealed record ProjectAuthorizationContext(
    IReadOnlySet<string> Permissions,
    bool IsOwner);
