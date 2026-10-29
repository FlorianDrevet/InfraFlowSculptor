namespace InfraFlowSculptor.Engine.Exposure;

public static class PilotExposureHandlers
{
    public static IReadOnlyList<IExposureHandler> Create() =>
    [
        new PublicExposureHandler(),
        new RestrictedExposureHandler()
    ];
}
