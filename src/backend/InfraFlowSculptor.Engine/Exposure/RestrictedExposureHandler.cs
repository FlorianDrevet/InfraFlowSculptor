using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Exposure;

public sealed class RestrictedExposureHandler : IExposureHandler
{
    public ExposureMode Key => ExposureMode.Restricted;

    public Exposure Build(IReadOnlyList<string> ranges) => new RestrictedExposure(ranges.ToImmutableArray());
}
