using System.Reflection;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TMessage, TResponse>(
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IOrganizationRoleAuthorizer roleAuthorizer)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requirement = typeof(TMessage).GetCustomAttribute<RequiresOrganizationRoleAttribute>();
        if (requirement is null)
        {
            return await next(message, cancellationToken);
        }

        if (currentOrganization.Id is not { } organizationId
            || !await roleAuthorizer.HasRoleAsync(
                currentUser.Key,
                organizationId,
                requirement.Role,
                cancellationToken))
        {
            var errors = new List<Error>
            {
                Error.Forbidden(
                    "ORG_ROLE_REQUIRED",
                    $"The {requirement.Role} organization role is required.",
                    new Dictionary<string, object> { ["permission"] = requirement.Role.ToString() })
            };
            return (dynamic)errors;
        }

        return await next(message, cancellationToken);
    }
}
