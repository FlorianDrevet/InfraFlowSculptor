using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Queries.GetProjectValidation;

public sealed class GetProjectValidationQueryHandler(
    IIfsDbContext dbContext,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ProjectValidationService validationService,
    FindingFixRegistry findingFixes)
    : IQueryHandler<GetProjectValidationQuery, ErrorOr<ProjectValidationSummary>>
{
    public async ValueTask<ErrorOr<ProjectValidationSummary>> Handle(
        GetProjectValidationQuery query,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(
            query.ProjectId,
            cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var snapshot = snapshotResult.Value;
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var validation = await validationService.GetAsync(snapshot, catalog, cancellationToken);
        var acknowledgments = await dbContext.Query<FindingAcknowledgmentAggregate>()
            .Where(acknowledgment => acknowledgment.ProjectId == new ProjectId(query.ProjectId))
            .ToArrayAsync(cancellationToken);
        var userIds = acknowledgments.Select(acknowledgment => acknowledgment.AcknowledgedBy).Distinct().ToArray();
        var users = userIds.Length == 0
            ? []
            : await dbContext.Query<UserProfile>()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, cancellationToken);

        var resources = snapshot.Components
            .SelectMany(component => component.Resources.Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(item => item.Resource.Id);
        var components = snapshot.Components.ToDictionary(component => component.Id);
        var appSettings = snapshot.AppSettings.ToDictionary(setting => setting.Id);
        var links = snapshot.Links.ToDictionary(link => link.Id);
        var environments = snapshot.Environments.ToDictionary(environment => environment.Id);

        var findings = validation.Findings.Select(finding =>
        {
            var acknowledgment = finding.ObjectVersion is { } objectVersion
                ? acknowledgments
                    .Where(candidate => candidate.FindingCode == finding.Code
                        && candidate.SubjectType == finding.Subject.Type
                        && candidate.SubjectId == finding.Subject.Id
                        && candidate.EnvironmentId == finding.EnvironmentId
                        && string.Equals(candidate.DetailKey, finding.DetailKey, StringComparison.Ordinal)
                        && candidate.ObjectVersion == objectVersion)
                    .MaxBy(candidate => candidate.CreatedAt)
                : null;
            var acknowledgmentSummary = acknowledgment is null
                ? null
                : new FindingAcknowledgmentSummary(
                    acknowledgment.Comment,
                    acknowledgment.AcknowledgedBy.Value,
                    users.TryGetValue(acknowledgment.AcknowledgedBy, out var user) ? user.DisplayName : "Utilisateur",
                    acknowledgment.CreatedAt);

            var subjectContext = ResolveSubjectContext(
                finding,
                snapshot,
                resources,
                components,
                appSettings,
                links,
                environments);

            return new ProjectValidationFindingSummary(
                finding.Code,
                finding.Severity.ToString(),
                finding.Lot,
                finding.Subject.Type.ToString(),
                finding.Subject.Id,
                finding.EnvironmentId,
                finding.MessageFr,
                finding.MessageEn,
                finding.ObjectVersion,
                finding.FixCode,
                finding.FixCode is { } fixCode && findingFixes.Supports(fixCode),
                finding.RelatedLinkIds,
                acknowledgmentSummary,
                finding.DetailKey,
                subjectContext.Label,
                subjectContext.ComponentId,
                subjectContext.ResourceId,
                finding.EnvironmentId is { } environmentId && environments.TryGetValue(environmentId, out var environment)
                    ? environment.Code
                    : null);
        }).ToArray();

        var orderedComponents = validation.OrderedComponents.Select((component, index) =>
        {
            var dependsOn = validation.Ordering.Wiring.Dependencies
                .Where(dependency => dependency.SourceComponentId == component.Id)
                .Select(dependency => dependency.TargetComponentId)
                .Distinct()
                .Where(components.ContainsKey)
                .Select(componentId => components[componentId].Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var usedBy = validation.Ordering.Wiring.Dependencies
                .Where(dependency => dependency.TargetComponentId == component.Id)
                .Select(dependency => dependency.SourceComponentId)
                .Distinct()
                .Where(components.ContainsKey)
                .Select(componentId => components[componentId].Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return new ProjectValidationComponentSummary(
                component.Id,
                component.Code,
                component.Name,
                index + 1,
                dependsOn,
                usedBy);
        }).ToArray();

        return new ProjectValidationSummary(
            validation.ProjectId,
            validation.ModelVersion,
            validation.CatalogVersion,
            findings.Count(finding => finding.Severity == "Error"),
            findings.Count(finding => finding.Severity == "ErrorAtPublish"),
            findings.Count(finding => finding.Severity == "Warning"),
            findings.Count(finding => finding.Severity == "Info"),
            findings.Count(finding => finding.Acknowledgment is not null),
            validation.Readiness,
            orderedComponents,
            findings);
    }

    private static FindingSubjectContext ResolveSubjectContext(
        InfraFlowSculptor.Engine.Validation.ValidationFinding finding,
        InfraFlowSculptor.Engine.Model.ModelSnapshot snapshot,
        Dictionary<Guid, (InfraFlowSculptor.Engine.Model.ComponentSnapshot Component, InfraFlowSculptor.Engine.Model.ResourceSnapshot Resource)> resources,
        Dictionary<Guid, InfraFlowSculptor.Engine.Model.ComponentSnapshot> components,
        Dictionary<Guid, InfraFlowSculptor.Engine.Model.AppSettingSnapshot> appSettings,
        Dictionary<Guid, InfraFlowSculptor.Engine.Links.LinkSnapshot> links,
        Dictionary<Guid, InfraFlowSculptor.Engine.Model.EnvironmentSnapshot> environments)
    {
        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.Resource
            && resources.TryGetValue(finding.Subject.Id, out var resource))
        {
            return new FindingSubjectContext(
                $"{resource.Component.Code} / {resource.Resource.LogicalName}",
                resource.Component.Id,
                resource.Resource.Id);
        }

        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.AppSetting
            && appSettings.TryGetValue(finding.Subject.Id, out var setting)
            && resources.TryGetValue(setting.ApplicationResourceId, out var application))
        {
            return new FindingSubjectContext(
                $"{application.Component.Code} / {application.Resource.LogicalName} · {setting.Name}",
                application.Component.Id,
                application.Resource.Id);
        }

        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.Link
            && links.TryGetValue(finding.Subject.Id, out var link)
            && resources.TryGetValue(link.SourceResourceId, out var source)
            && resources.TryGetValue(link.TargetResourceId, out var target))
        {
            return new FindingSubjectContext(
                $"{source.Component.Code} / {source.Resource.LogicalName} → {target.Component.Code} / {target.Resource.LogicalName}",
                source.Component.Id,
                source.Resource.Id);
        }

        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.Component
            && components.TryGetValue(finding.Subject.Id, out var component))
        {
            return new FindingSubjectContext(component.Code, component.Id, null);
        }

        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.Environment
            && environments.TryGetValue(finding.Subject.Id, out var environment))
        {
            return new FindingSubjectContext(environment.Code, null, null);
        }

        if (finding.Subject.Type == InfraFlowSculptor.Domain.Common.ObjectType.Project)
        {
            return new FindingSubjectContext(snapshot.Project.Code, null, null);
        }

        return new FindingSubjectContext(finding.Subject.Type.ToString(), null, null);
    }

    private sealed record FindingSubjectContext(string Label, Guid? ComponentId, Guid? ResourceId);
}
