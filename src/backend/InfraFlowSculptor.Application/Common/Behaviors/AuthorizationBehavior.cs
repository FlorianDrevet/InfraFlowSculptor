using System.Reflection;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Mediator;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TMessage, TResponse>(
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IOrganizationRoleAuthorizer roleAuthorizer,
    IProjectAuthorizer projectAuthorizer)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var permissionRequirement = typeof(TMessage).GetCustomAttribute<RequiresPermissionAttribute>();
        if (permissionRequirement is not null)
        {
            if (message is not IProjectScopedRequest projectScopedRequest)
            {
                throw new InvalidOperationException(
                    $"{typeof(TMessage).Name} requires a project identifier to authorize its permission.");
            }

            if (currentOrganization.Id is not { } currentOrganizationId)
            {
                return (dynamic)new List<Error>
                {
                    Error.NotFound("NOT_FOUND", "The project was not found.")
                };
            }

            var projectAuthorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
                currentUser.Key,
                currentOrganizationId,
                new ProjectId(projectScopedRequest.ProjectId),
                cancellationToken);
            if (projectAuthorization is null)
            {
                return (dynamic)new List<Error>
                {
                    Error.NotFound("NOT_FOUND", "The project was not found.")
                };
            }

            if (!projectAuthorization.Permissions.Contains(permissionRequirement.Permission))
            {
                return (dynamic)new List<Error>
                {
                    Error.Forbidden(
                        "PROJECT_PERMISSION_REQUIRED",
                        $"The project permission '{permissionRequirement.Permission}' is required.",
                        new Dictionary<string, object> { ["permission"] = permissionRequirement.Permission })
                };
            }
        }

        var organizationRequirement = typeof(TMessage).GetCustomAttribute<RequiresOrganizationRoleAttribute>();
        if (organizationRequirement is not null)
        {
            var hasRequiredRole = false;
            if (currentOrganization.Id is { } organizationId)
            {
                foreach (var role in organizationRequirement.Roles)
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
                var permission = organizationRequirement.Permission ?? string.Join(", ", organizationRequirement.Roles);
                var errors = new List<Error>
                {
                    Error.Forbidden(
                        "ORG_ROLE_REQUIRED",
                        $"One of the following organization roles is required: {string.Join(", ", organizationRequirement.Roles)}.",
                        new Dictionary<string, object> { ["permission"] = permission })
                };
                return (dynamic)errors;
            }
        }

        return await next(message, cancellationToken);
    }
}
