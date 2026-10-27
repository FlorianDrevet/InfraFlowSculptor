using FluentValidation;

namespace InfraFlowSculptor.Application.Organizations.Commands.AcceptInvitation;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .Length(43);
    }
}
