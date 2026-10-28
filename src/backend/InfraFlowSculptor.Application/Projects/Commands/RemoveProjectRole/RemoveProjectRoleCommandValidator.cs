using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;

public sealed class RemoveProjectRoleCommandValidator : AbstractValidator<RemoveProjectRoleCommand>
{
    public RemoveProjectRoleCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
        RuleFor(command => command.Scope).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
