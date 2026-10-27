using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Wizard;

[RequiresOrganizationRole(OrganizationRole.Administrator, OrganizationRole.Member)]
public sealed record DiscardWizardDraftCommand : ICommand<ErrorOr<Success>>;
