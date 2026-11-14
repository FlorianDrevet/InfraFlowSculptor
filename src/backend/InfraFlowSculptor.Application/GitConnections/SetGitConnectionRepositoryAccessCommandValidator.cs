using FluentValidation;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class SetGitConnectionRepositoryAccessCommandValidator
    : AbstractValidator<SetGitConnectionRepositoryAccessCommand>
{
    public SetGitConnectionRepositoryAccessCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ConnectionId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ProviderRepositoryId).NotEmpty().MaximumLength(255)
            .WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ProjectIds).NotNull().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ProjectIds)
            .Must(projectIds => projectIds.Count == 0)
            .When(command => command.OpenToAllProjects)
            .WithErrorCode("VAL-PUB-CONNEXION");
    }
}
