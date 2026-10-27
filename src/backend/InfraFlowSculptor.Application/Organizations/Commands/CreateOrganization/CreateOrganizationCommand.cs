using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Domain.Common;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;

[Audited(AuditActions.OrganizationCreated, ObjectType.Organization)]
public sealed record CreateOrganizationCommand(string Name) : ICommand<ErrorOr<OrganizationSummary>>;
