using System.Security.Claims;

namespace InfraFlowSculptor.Application.Common.Security;

public interface IVerifiedEmailResolver
{
    VerifiedEmail? Resolve(ClaimsPrincipal principal);
}
