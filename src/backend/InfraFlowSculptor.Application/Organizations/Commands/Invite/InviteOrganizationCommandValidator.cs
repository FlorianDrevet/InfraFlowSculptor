using FluentValidation;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations.Commands.Invite;

public sealed class InviteOrganizationCommandValidator : AbstractValidator<InviteOrganizationCommand>
{
    public InviteOrganizationCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Roles)
            .NotEmpty()
            .Must(roles => roles.All(Enum.IsDefined))
            .WithMessage("Only supported organization roles may be assigned.");
    }
}
