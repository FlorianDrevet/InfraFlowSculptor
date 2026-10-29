using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class SqlAdministratorMembershipRule : IValidationRule
{
    public string Code => "VAL-LIA-SQL-ADMIN";

    public FindingSeverity Severity => FindingSeverity.Warning;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context)
    {
        var resources = context.Resources.ToDictionary(item => item.Resource.Id);
        var findingsByServer = new Dictionary<Guid, (ResourceSnapshot Server, SortedSet<string> DatabaseNames, SortedSet<Guid> RelatedLinkIds)>();
        foreach (var request in context.Ordering.Wiring.DataAccessRequests)
        {
            if (!resources.TryGetValue(request.TargetResourceId, out var database))
            {
                continue;
            }

            var hostingLink = context.Ordering.Wiring.Links
                .Where(wired => wired.Link.SourceResourceId == database.Resource.Id)
                .Where(wired => context.LinkKinds.Get(wired.Link.Kind).IsResourceHostingRelationship)
                .Select(wired => wired.Link)
                .FirstOrDefault();
            if (hostingLink is null || !resources.TryGetValue(hostingLink.TargetResourceId, out var server))
            {
                continue;
            }

            if (!findingsByServer.TryGetValue(server.Resource.Id, out var finding))
            {
                finding = (
                    server.Resource,
                    new SortedSet<string>(StringComparer.Ordinal),
                    new SortedSet<Guid>());
            }

            finding.DatabaseNames.Add(database.Resource.LogicalName);
            finding.RelatedLinkIds.Add(request.Origin.LinkId);
            finding.RelatedLinkIds.Add(hostingLink.Id);
            findingsByServer[server.Resource.Id] = finding;
        }

        return findingsByServer.Values
            .OrderBy(finding => finding.Server.LogicalName, StringComparer.Ordinal)
            .Select(finding =>
            {
                var databaseNamesFr = string.Join(" », « ", finding.DatabaseNames);
                var databaseNamesEn = string.Join("', '", finding.DatabaseNames);
                return context.Finding(
                    Code,
                    Severity,
                    new ObjectRef(ObjectType.Resource, finding.Server.Id),
                    $"L’accès aux données de « {databaseNamesFr} » est configuré, mais l’appartenance de l’identité de déploiement au groupe administrateur SQL n’est pas confirmée dans la liste de contrôle.",
                    $"Data access to '{databaseNamesEn}' is configured, but deployment identity membership in the SQL administrator group has not been confirmed in the checklist.",
                    relatedLinkIds: finding.RelatedLinkIds.ToArray(),
                    objectVersion: GetAcknowledgmentVersion(context.Snapshot.Project.ModelVersion));
            })
            .ToArray();
    }

    // This finding depends on links as well as the SQL server, so its acknowledgment follows the model context version.
    private static int GetAcknowledgmentVersion(int modelVersion) =>
        modelVersion == int.MaxValue ? int.MaxValue : Math.Max(1, modelVersion + 1);
}
