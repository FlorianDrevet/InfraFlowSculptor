using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.RemoveMember;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.OrganizationMemberRemoved, ObjectType.OrganizationMember)]
public sealed record RemoveMemberCommand(Guid OrganizationId, Guid MemberId) : ICommand<ErrorOr<Success>>;
