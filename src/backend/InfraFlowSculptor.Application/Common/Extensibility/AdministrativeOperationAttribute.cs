namespace InfraFlowSculptor.Application.Common.Extensibility;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AdministrativeOperationAttribute : Attribute
{
}
