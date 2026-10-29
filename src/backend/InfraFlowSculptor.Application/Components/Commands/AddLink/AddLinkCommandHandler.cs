using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.AddLink;

public sealed class AddLinkCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider,
    LinkKindRegistry handlers)
    : ICommandHandler<AddLinkCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        AddLinkCommand command,
        CancellationToken cancellationToken)
    {
        var validated = await LinkCommandSupport.LoadAndValidateAsync(
            dbContext, currentOrganization, catalogProvider, handlers,
            command.ProjectId, command.ComponentId, command.SourceResourceId,
            command.TargetComponentId, command.TargetResourceId, command.Kind,
            command.IdentityKind, command.IdentityResourceId, command.Roles,
            command.AccessScope, command.DataAccessLevel, null, command.ExpectedComponentVersion, cancellationToken);
        if (validated.IsError)
        {
            return validated.Errors;
        }

        var created = LinkAggregate.Create(
            command.LinkId,
            new ProjectId(command.ProjectId),
            validated.Value.Project.OrganizationId,
            command.ComponentId,
            command.SourceResourceId,
            command.TargetComponentId,
            command.TargetResourceId,
            command.Kind,
            command.IdentityKind,
            command.IdentityResourceId,
            command.Roles,
            command.AccessScope,
            command.DataAccessLevel);
        if (created.IsError)
        {
            return created.Errors;
        }

        dbContext.Query<LinkAggregate>().Add(created.Value);
        LinkCommandSupport.RecordComponentLinkChanges(
            validated.Value.SourceComponent,
            validated.Value.TargetComponent);
        return new ModelCommandOutcome("Link created.", []);
    }
}
