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

        var hasRequiredRole = false;
        if (currentOrganization.Id is { } organizationId)
        {
            foreach (var role in requirement.Roles)
            {
                if (await roleAuthorizer.HasRoleAsync(
                    currentUser.Key,
                    organizationId,
                    role,
                    cancellationToken))
                {
                    hasRequiredRole = true;
                    break;
                }
            }
        }

        if (!hasRequiredRole)
        {
            var permission = requirement.Permission ?? string.Join(", ", requirement.Roles);
            var errors = new List<Error>
            {
                Error.Forbidden(
                    "ORG_ROLE_REQUIRED",
                    $"One of the following organization roles is required: {string.Join(", ", requirement.Roles)}.",
                    new Dictionary<string, object> { ["permission"] = permission })
            };
            return (dynamic)errors;
        }

        return await next(message, cancellationToken);
    }
}
