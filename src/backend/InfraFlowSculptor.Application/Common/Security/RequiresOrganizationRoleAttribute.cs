using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Common.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresOrganizationRoleAttribute(OrganizationRole role) : Attribute
{
    public OrganizationRole Role { get; } = role;
}
