using InfraFlowSculptor.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace InfraFlowSculptor.Api.Tests.Controllers;

public sealed class DevelopmentControllerTests
{
    [Fact]
    public async Task ExpiryAlertAcceptedResultReturnsItsMappedStatusLocation()
    {
        var jobId = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        var context = new DefaultHttpContext { RequestServices = app.Services };

        await DevelopmentController.CreateExpiryAlertAcceptedResult(jobId).ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal($"/v1/dev/git-connections/expiry-alerts/{jobId:D}", context.Response.Headers.Location.ToString());
    }
}
