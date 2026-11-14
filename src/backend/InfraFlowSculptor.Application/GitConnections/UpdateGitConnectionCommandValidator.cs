using FluentValidation;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class UpdateGitConnectionCommandValidator : AbstractValidator<UpdateGitConnectionCommand>
{
    public UpdateGitConnectionCommandValidator(TimeProvider clock)
    {
        RuleFor(command => command.OrganizationId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ConnectionId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(80).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.Organization).NotEmpty().MaximumLength(300).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.PersonalAccessTokenExpiresAt)
            .NotNull()
            .GreaterThan(clock.GetUtcNow())
            .When(command => command.PersonalAccessToken is not null || command.PersonalAccessTokenExpiresAt is not null)
            .WithErrorCode("VAL-PUB-CONNEXION");
    }
}
