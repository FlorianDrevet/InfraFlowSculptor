using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.RemoveMember;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record RemoveMemberCommand(Guid OrganizationId, Guid MemberId) : ICommand<ErrorOr<Success>>;
