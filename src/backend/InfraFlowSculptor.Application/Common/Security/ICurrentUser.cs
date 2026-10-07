namespace InfraFlowSculptor.Application.Common.Security;

public interface ICurrentUser
{
    UserKey Key { get; }

    string DisplayName { get; }

    VerifiedEmail? VerifiedEmail { get; }

    bool IsAuthenticated { get; }

    AuthenticationKind AuthenticationKind { get; }
}
