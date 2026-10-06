using System.Diagnostics;

namespace InfraFlowSculptor.Worker.Jobs;

public static class JobActivity
{
    public const string SourceName = "InfraFlowSculptor.Jobs";

    public static ActivitySource Source { get; } = new(SourceName);
}
