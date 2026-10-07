namespace InfraFlowSculptor.Application.Common.Extensibility;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TokenScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}
