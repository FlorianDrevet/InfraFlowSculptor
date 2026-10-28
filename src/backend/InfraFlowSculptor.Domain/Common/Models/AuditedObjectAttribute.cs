using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Domain.Common.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditedObjectAttribute(
    ObjectType objectType,
    string idProperty,
    string organizationIdProperty) : Attribute
{
    public ObjectType ObjectType { get; } = objectType;

    public string IdProperty { get; } = idProperty;

    public string OrganizationIdProperty { get; } = organizationIdProperty;
}
