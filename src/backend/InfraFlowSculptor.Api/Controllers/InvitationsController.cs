using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Application.Organizations.Commands.AcceptInvitation;
using InfraFlowSculptor.Application.Organizations.Commands.Invite;
using InfraFlowSculptor.Application.Organizations.Commands.ResendInvitation;
using InfraFlowSculptor.Application.Organizations.Commands.RevokeInvitation;
using InfraFlowSculptor.Application.Organizations.Queries.GetInvitationPreview;
using InfraFlowSculptor.Application.Organizations.Queries.ListInvitations;
using InfraFlowSculptor.Contracts.Organizations;
using InfraFlowSculptor.Domain.Common.Features;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class InvitationsController
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/invitations/{token}", async (
                string token,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetInvitationPreviewQuery(token), cancellationToken);
                return result.Match<IResult>(
                    preview => Results.Ok(new InvitationPreviewResponse(
                        preview.OrganizationId,
                        preview.OrganizationName,
                        preview.Email,
                        preview.Roles,
                        preview.Status,
                        preview.CreatedAt,
                        preview.ExpiresAt,
                        preview.EmailVerified,
                        preview.EmailMatches,
                        preview.TenantAllowed)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetInvitationPreview)
            .WithSummary("Afficher les détails d'une invitation")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<InvitationPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var invitations = v1.MapGroup("/organizations/{id:guid}/invitations")
            .WithTags("Organizations")
            .RequireFeature(Features.Organizations);

        invitations.MapGet("/", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListInvitationsQuery(id), cancellationToken);
                return result.Match<IResult>(
                    items => Results.Ok(items.Select(Map)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListOrganizationInvitations)
            .WithSummary("Afficher les invitations de l'organisation")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<InvitationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        invitations.MapPost("/", async (
                Guid id,
                InviteOrganizationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new InviteOrganizationCommand(id, request.Email, request.Roles),
                    cancellationToken);
                return result.Match<IResult>(
                    invitation => Results.Created(
                        $"/v1/organizations/{id:D}/invitations/{invitation.Id:D}",
                        Map(invitation)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.InviteOrganizationMember)
            .WithSummary("Inviter un membre par e-mail (UC-ORG-03)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<InvitationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        invitations.MapPost("/{invitationId:guid}/resend", async (
                Guid id,
                Guid invitationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ResendInvitationCommand(id, invitationId), cancellationToken);
                return result.Match<IResult>(
                    invitation => Results.Ok(Map(invitation)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ResendOrganizationInvitation)
            .WithSummary("Renvoyer une invitation (UC-ORG-05)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<InvitationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        invitations.MapDelete("/{invitationId:guid}", async (
                Guid id,
                Guid invitationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new RevokeInvitationCommand(id, invitationId), cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RevokeOrganizationInvitation)
            .WithSummary("Révoquer une invitation (UC-ORG-05)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        v1.MapPost("/invitations/{token}/accept", async (
                string token,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new AcceptInvitationCommand(token), cancellationToken);
                return result.Match<IResult>(
                    invitation => Results.Ok(new InvitationAcceptanceResponse(
                        invitation.OrganizationId,
                        invitation.OrganizationName,
                        invitation.Email,
                        invitation.Roles)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.AcceptInvitation)
            .WithSummary("Accepter une invitation avec une adresse vérifiée (UC-ORG-04)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<InvitationAcceptanceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return invitations;
    }

    private static InvitationResponse Map(InvitationSummary invitation) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.Roles,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt);
}
