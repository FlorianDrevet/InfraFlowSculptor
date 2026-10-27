using ErrorOr;
using InfraFlowSculptor.Application.Common.Behaviors;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;
using NSubstitute;
using Xunit;

namespace InfraFlowSculptor.Application.Tests.Security;

public sealed class ProjectAuthorizationBehaviorTests
{
    private static readonly OrganizationId ActiveOrganizationId = new(Guid.NewGuid());
    private static readonly UserKey CurrentUserKey = new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void AuthorizationBehaviorDependsOnProjectAuthorizerForPermissionChecks()
    {
        var parameterTypes = typeof(AuthorizationBehavior<,>)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.FullName)
            .ToArray();

        Assert.Contains(
            "InfraFlowSculptor.Application.Common.Security.IProjectAuthorizer",
            parameterTypes);
    }

    [Fact]
    public async Task ProjectWithoutRoleIsHiddenAsNotFound()
    {
        var projectAuthorizer = CreateProjectAuthorizer(null);
        var behavior = CreateBehavior<ProjectReadRequest>(projectAuthorizer);
        var nextWasCalled = false;

        var result = await behavior.Handle(
            new ProjectReadRequest(Guid.NewGuid()),
            (_, _) =>
            {
                nextWasCalled = true;
                return ValueTask.FromResult<ErrorOr<Success>>(Result.Success);
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Type == ErrorType.NotFound);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task VisibleProjectWithoutPermissionReturnsForbiddenAndNamesPermission()
    {
        var permissions = new HashSet<string>([Permission.ProjectRead], StringComparer.Ordinal);
        var projectAuthorizer = CreateProjectAuthorizer(new ProjectAuthorizationContext(permissions, IsOwner: false));
        var behavior = CreateBehavior<MemberManagementRequest>(projectAuthorizer);

        var result = await behavior.Handle(
            new MemberManagementRequest(Guid.NewGuid()),
            (_, _) => ValueTask.FromResult<ErrorOr<Success>>(Result.Success),
            CancellationToken.None);

        Assert.True(result.IsError);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal(Permission.MembersManage, error.Metadata!["permission"]);
    }

    [Fact]
    public async Task UserWithPermissionContinuesToHandler()
    {
        var permissions = new HashSet<string>([Permission.ProjectRead], StringComparer.Ordinal);
        var projectAuthorizer = CreateProjectAuthorizer(new ProjectAuthorizationContext(permissions, IsOwner: false));
        var behavior = CreateBehavior<ProjectReadRequest>(projectAuthorizer);
        var nextWasCalled = false;

        var result = await behavior.Handle(
            new ProjectReadRequest(Guid.NewGuid()),
            (_, _) =>
            {
                nextWasCalled = true;
                return ValueTask.FromResult<ErrorOr<Success>>(Result.Success);
            },
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(nextWasCalled);
    }

    private static AuthorizationBehavior<TRequest, ErrorOr<Success>> CreateBehavior<TRequest>(
        IProjectAuthorizer projectAuthorizer)
        where TRequest : IProjectScopedRequest, ICommand<ErrorOr<Success>>
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Key.Returns(CurrentUserKey);
        var currentOrganization = Substitute.For<ICurrentOrganization>();
        currentOrganization.Id.Returns(ActiveOrganizationId);
        return new AuthorizationBehavior<TRequest, ErrorOr<Success>>(
            currentUser,
            currentOrganization,
            Substitute.For<IOrganizationRoleAuthorizer>(),
            projectAuthorizer);
    }

    private static IProjectAuthorizer CreateProjectAuthorizer(ProjectAuthorizationContext? context)
    {
        var authorizer = Substitute.For<IProjectAuthorizer>();
        authorizer.GetProjectAuthorizationContextAsync(
                CurrentUserKey,
                ActiveOrganizationId,
                Arg.Any<ProjectId>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(context));
        return authorizer;
    }

    [RequiresPermission(Permission.ProjectRead)]
    private sealed record ProjectReadRequest(Guid ProjectId)
        : ICommand<ErrorOr<Success>>, IProjectScopedRequest;

    [RequiresPermission(Permission.MembersManage)]
    private sealed record MemberManagementRequest(Guid ProjectId)
        : ICommand<ErrorOr<Success>>, IProjectScopedRequest;
}
