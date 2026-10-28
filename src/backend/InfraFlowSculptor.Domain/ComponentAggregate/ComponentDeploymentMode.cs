using System.Diagnostics.CodeAnalysis;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public enum ComponentDeploymentMode
{
    PerEnvironment,
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The value follows the deployment mode defined in specification 12.")]
    Single
}
