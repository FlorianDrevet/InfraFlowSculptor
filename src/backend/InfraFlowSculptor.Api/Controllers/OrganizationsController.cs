using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;
using InfraFlowSculptor.Application.Organizations.Commands.RenameOrganization;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Application.Organizations.Queries.GetOrganization;
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

        return organizations;
    }

    private static OrganizationSummaryResponse Map(OrganizationSummary organization) =>
        new(organization.Id, organization.Name, organization.Roles, organization.Version);
}
