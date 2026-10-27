using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Organizations.Commands.ChangeMemberRoles;
using InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;
using InfraFlowSculptor.Application.Organizations.Commands.LeaveOrganization;
using InfraFlowSculptor.Application.Organizations.Commands.RemoveMember;
using InfraFlowSculptor.Application.Organizations.Commands.RenameOrganization;
using InfraFlowSculptor.Application.Organizations.Commands.UpdateAccessRestrictions;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Application.Organizations.Queries.GetOrganization;
using InfraFlowSculptor.Application.Organizations.Queries.ListOrganizationMembers;
using InfraFlowSculptor.Application.Organizations.Queries.ListAuditEvents;
using InfraFlowSculptor.Application.Organizations.Queries.ListMyOrganizations;
using InfraFlowSculptor.Contracts.Organizations;
using InfraFlowSculptor.Domain.Common.Features;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class OrganizationsController
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder v1)
    {
        var organizations = v1.MapGroup("/organizations")
            .WithTags("Organizations")
            .RequireFeature(Features.Organizations);

        organizations.MapGet("/", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListMyOrganizationsQuery(), cancellationToken);
                return result.Match<IResult>(
                    items => Results.Ok(items.Select(Map)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListMyOrganizations)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<IReadOnlyList<OrganizationSummaryResponse>>(StatusCodes.Status200OK);

        organizations.MapPost("/", async (
                CreateOrganizationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new CreateOrganizationCommand(request.Name), cancellationToken);
                return result.Match<IResult>(
                    organization => Results.Created(
                        $"/v1/organizations/{organization.Id:D}",
                        Map(organization)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateOrganization)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<OrganizationSummaryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        organizations.MapGet("/{id:guid}", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetOrganizationQuery(id), cancellationToken);
                return result.Match<IResult>(
                    organization => Results.Ok(Map(organization)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetOrganization)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<OrganizationSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organizations.MapPut("/{id:guid}", async (
                Guid id,
                RenameOrganizationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new RenameOrganizationCommand(id, request.Name), cancellationToken);
                return result.Match<IResult>(
                    organization => Results.Ok(Map(organization)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RenameOrganization)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .Produces<OrganizationSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        organizations.MapGet("/{id:guid}/members", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListOrganizationMembersQuery(id), cancellationToken);
                return result.Match<IResult>(
                    members => Results.Ok(members.Select(Map)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListOrganizationMembers)
            .WithSummary("Afficher les membres de l'organisation")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<OrganizationMemberResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        organizations.MapGet("/{id:guid}/audit", async (
                Guid id,
                DateTimeOffset? from,
                DateTimeOffset? to,
                int? page,
                int? pageSize,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new ListAuditEventsQuery(
                        id,
                        from,
                        to,
                        page ?? 1,
                        pageSize ?? AuditPageLimits.DefaultPageSize),
                    cancellationToken);
                return result.Match<IResult>(
                    auditPage => Results.Ok(new AuditEventPageResponse(
                        auditPage.Items.Select(Map).ToArray(),
                        auditPage.Page,
                        auditPage.PageSize,
                        auditPage.TotalCount)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListOrganizationAuditEvents)
            .WithSummary("Consulter le journal d’audit de l’organisation (UC-ORG-19)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<AuditEventPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        organizations.MapPut("/{id:guid}/members/{memberId:guid}/roles", async (
                Guid id,
                Guid memberId,
                ChangeOrganizationMemberRolesRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new ChangeMemberRolesCommand(id, memberId, request.Roles),
                    cancellationToken);
                return result.Match<IResult>(
                    member => Results.Ok(Map(member)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ChangeOrganizationMemberRoles)
            .WithSummary("Modifier les rôles d'organisation d'un membre (UC-ORG-11)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<OrganizationMemberResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organizations.MapDelete("/{id:guid}/members/{memberId:guid}", async (
                Guid id,
                Guid memberId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new RemoveMemberCommand(id, memberId), cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RemoveOrganizationMember)
            .WithSummary("Retirer un membre de l'organisation (UC-ORG-12)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organizations.MapPost("/{id:guid}/leave", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new LeaveOrganizationCommand(id), cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.LeaveOrganization)
            .WithSummary("Quitter une organisation (UC-ORG-13)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organizations.MapPut("/{id:guid}/access-restrictions", async (
                Guid id,
                UpdateOrganizationAccessRestrictionsRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new UpdateAccessRestrictionsCommand(
                        id,
                        request.AllowedEmailDomains,
                        request.AllowedTenantIds),
                    cancellationToken);
                return result.Match<IResult>(
                    organization => Results.Ok(Map(organization)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateOrganizationAccessRestrictions)
            .WithSummary("Modifier les domaines et tenants autorisés (RG-ORG-06)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<OrganizationSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return organizations;
    }

    private static OrganizationSummaryResponse Map(OrganizationSummary organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.Roles,
            organization.Version,
            organization.AllowedEmailDomains,
            organization.AllowedTenantIds);

    private static OrganizationMemberResponse Map(OrganizationMemberSummary member) =>
        new(member.Id, member.DisplayName, member.Email, member.Roles, member.IsOutsideAccessRestrictions);

    private static AuditEventResponse Map(AuditEventItem auditEvent) =>
        new(
            auditEvent.Id,
            auditEvent.OccurredAt,
            auditEvent.ActorDisplayName,
            auditEvent.Via.ToString(),
            auditEvent.Action,
            auditEvent.ObjectType,
            auditEvent.ObjectId,
            new AuditEventDiffResponse(auditEvent.Diff.Changes.Select(change => new AuditPropertyChangeResponse(
                change.Name,
                change.Before,
                change.After,
                change.IsPersonalData)).ToArray()));
}
