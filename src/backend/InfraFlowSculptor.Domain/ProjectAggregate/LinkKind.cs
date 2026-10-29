namespace InfraFlowSculptor.Domain.ProjectAggregate;

public enum LinkKind
{
    Hosting,
    Logging,
    Diagnostics,
    Telemetry,
    ImagePull,
    HostStorage,
    ConfigurationRead,
    Access,
    DataAccess,
    ValueDependency,
    SecretRead
}
