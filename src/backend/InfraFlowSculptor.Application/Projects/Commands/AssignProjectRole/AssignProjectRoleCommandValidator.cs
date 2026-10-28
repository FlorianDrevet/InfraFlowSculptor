using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.AssignProjectRole;

public sealed class AssignProjectRoleCommandValidator : AbstractValidator<AssignProjectRoleCommand>
{
    public AssignProjectRoleCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
        RuleFor(command => command.Scope).IsInEnum();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
