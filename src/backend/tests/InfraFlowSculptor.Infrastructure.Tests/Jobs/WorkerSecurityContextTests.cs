using AwesomeAssertions;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class WorkerSecurityContextTests
{
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
