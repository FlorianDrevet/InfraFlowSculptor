using AwesomeAssertions;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class WorkerSecurityContextTests
{
    [Fact]
    public void WorkerContextRegistersProjectAuthorizationServices()
    {
        var services = new ServiceCollection();
        services.AddWorkerSecurityContext();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IProjectAuthorizer)
            && descriptor.ImplementationType == typeof(EfProjectAuthorizer));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IProjectOwnershipCheck)
            && descriptor.ImplementationType == typeof(EfProjectOwnershipCheck));
    }

    [Fact]
    public void WorkerContextHasNoAuthenticatedHttpUser()
    {
        var services = new ServiceCollection();
        services.AddWorkerSecurityContext();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        currentUser.IsAuthenticated.Should().BeFalse();
        currentUser.Key.IsEmpty.Should().BeTrue();
        currentUser.VerifiedEmail.Should().BeNull();
    }
}
