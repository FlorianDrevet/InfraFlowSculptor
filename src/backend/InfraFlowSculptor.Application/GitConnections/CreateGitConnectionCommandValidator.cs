using FluentValidation;
using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class CreateGitConnectionCommandValidator : AbstractValidator<CreateGitConnectionCommand>
{
    public CreateGitConnectionCommandValidator(TimeProvider clock)
    {
        RuleFor(command => command.OrganizationId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ConnectionId).NotEmpty().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(80).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.Organization).NotEmpty().MaximumLength(300).WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.ProviderKind).IsInEnum().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.AuthenticationMode).IsInEnum().WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.TenantId)
            .NotNull()
            .When(command => command.AuthenticationMode == GitConnectionAuthenticationMode.FederatedServicePrincipal)
            .WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.PersonalAccessToken)
            .NotEmpty()
            .When(command => command.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken)
            .WithErrorCode("VAL-PUB-CONNEXION");
        RuleFor(command => command.PersonalAccessTokenExpiresAt)
            .NotNull()
            .GreaterThan(clock.GetUtcNow())
            .When(command => command.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken)
            .WithErrorCode("VAL-PUB-CONNEXION");
    }
}
