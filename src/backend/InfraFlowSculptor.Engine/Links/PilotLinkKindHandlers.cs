namespace InfraFlowSculptor.Engine.Links;

public static class PilotLinkKindHandlers
{
    public static IReadOnlyList<ILinkKindHandler> Create() =>
    [
        new HostingLinkKindHandler(),
        new LoggingLinkKindHandler(),
        new DiagnosticsLinkKindHandler(),
        new TelemetryLinkKindHandler(),
        new ImagePullLinkKindHandler(),
        new AccessLinkKindHandler(),
        new DataAccessLinkKindHandler(),
        new ValueDependencyLinkKindHandler(),
        new SecretReadLinkKindHandler()
    ];
}
