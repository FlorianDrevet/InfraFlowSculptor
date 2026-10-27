using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;

public sealed record CreateOrganizationCommand(string Name) : ICommand<ErrorOr<OrganizationSummary>>;
