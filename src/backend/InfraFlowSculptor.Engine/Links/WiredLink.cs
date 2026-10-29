namespace InfraFlowSculptor.Engine.Links;

public sealed record WiredLink(LinkSnapshot Link, bool IsExternal, bool CreatesDeploymentDependency);
