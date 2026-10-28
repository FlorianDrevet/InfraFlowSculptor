using FluentValidation;

namespace InfraFlowSculptor.Application.Organizations.Commands.ChangeMemberRoles;

public sealed class ChangeMemberRolesCommandValidator : AbstractValidator<ChangeMemberRolesCommand>
{
    public ChangeMemberRolesCommandValidator()
    {
        RuleFor(command => command.Roles)
            .NotEmpty()
            .WithErrorCode("ORG_MEMBER_ROLE_REQUIRED");
        RuleForEach(command => command.Roles)
            .IsInEnum()
            .WithErrorCode("ORG_MEMBER_ROLE_INVALID");
    }
}
