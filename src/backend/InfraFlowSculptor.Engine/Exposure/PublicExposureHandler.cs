namespace InfraFlowSculptor.Engine.Exposure;

public sealed class PublicExposureHandler : IExposureHandler
{
    public ExposureMode Key => ExposureMode.Public;

    public Exposure Build(IReadOnlyList<string> ranges) => new PublicExposure();
}
