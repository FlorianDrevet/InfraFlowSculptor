using System.Diagnostics;
using OpenTelemetry;

namespace InfraFlowSculptor.Api.Observability;

internal sealed class ProjectExportTelemetryRedactionProcessor : BaseProcessor<Activity>
{
    private const string ProjectExportPathPrefix = "/v1/project-exports/";

    public override void OnEnd(Activity activity)
    {
        if (activity.GetTagItem("url.path") is not string path
            || !path.StartsWith(ProjectExportPathPrefix, StringComparison.Ordinal))
        {
            return;
        }

        activity.SetTag("url.path", $"{ProjectExportPathPrefix}{{token}}");
    }
}
