using FluentValidation;

namespace InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length is >= 2 and <= 80)
            .WithErrorCode("ORG_NAME_INVALID")
            .WithMessage("Organization name must contain between 2 and 80 characters.");
    }
}
