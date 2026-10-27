using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.LeaveOrganization;

public sealed record LeaveOrganizationCommand(Guid OrganizationId) : ICommand<ErrorOr<Success>>;
