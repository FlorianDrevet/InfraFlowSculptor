using System.Diagnostics;
using InfraFlowSculptor.Api.Observability;

namespace InfraFlowSculptor.Api.Tests.Observability;

public sealed class ProjectExportTelemetryRedactionTests
{
    [Fact]
    public void ProjectExportPathDoesNotExposeItsBearerTokenInTelemetry()
    {
        const string token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var activity = new Activity("HTTP GET");
        activity.SetTag("url.path", $"/v1/project-exports/{token}");

        new ProjectExportTelemetryRedactionProcessor().OnEnd(activity);

        Assert.Equal("/v1/project-exports/{token}", activity.GetTagItem("url.path"));
    }

    [Fact]
    public void UnrelatedPathsRemainUnchanged()
    {
        const string path = "/v1/projects/77777777-7777-4777-8777-777777777777";
        using var activity = new Activity("HTTP GET");
        activity.SetTag("url.path", path);

        new ProjectExportTelemetryRedactionProcessor().OnEnd(activity);

        Assert.Equal(path, activity.GetTagItem("url.path"));
    }
}
