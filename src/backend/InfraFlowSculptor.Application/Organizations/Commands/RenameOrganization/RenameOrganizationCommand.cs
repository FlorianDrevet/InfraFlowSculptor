using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.RenameOrganization;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record RenameOrganizationCommand(Guid Id, string Name) : ICommand<ErrorOr<OrganizationSummary>>;
