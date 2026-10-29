using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class WiringEngine(LinkKindRegistry linkKinds, DataAccessProviderRegistry dataAccessProviders)
{
    private const string LinkNotFoundCode = "VAL-LIA-RESSOURCE-INTROUVABLE";
    private const string LinkSelfCode = "RG-LIA-02";
    private const string ExistingSourceCode = "VAL-LIA-SOURCE-EXISTING";
    private const string LinkKindUnsupportedCode = "VAL-LIA-TYPE";
    private const string ImplicitOnlyCode = "VAL-LIA-IMPLICITE";
    private const string IdentityRequiredCode = "RG-LIA-08-IDENTITY-REQUIRED";
    private const string IdentityUnsupportedCode = "VAL-LIA-IDENTITE";
    private const string SharedIdentityCode = "VAL-LIA-IDENTITE-PARTAGEE";
    private const string IdentityCapacityCode = "VAL-LIA-IDENTITE-CAPACITE";
    private const string RoleUnsupportedCode = "VAL-LIA-ROLE";
    private const string DataAccessUnsupportedCode = "VAL-LIA-DATA-ENGINE";
    private const string DataAccessLevelRequiredCode = "VAL-LIA-DATA-LEVEL";
    private const string DataAccessLevelFieldName = "level";
    private const string LinkCardinalityCode = "VAL-LIA-CARDINALITE";
    private const string LinkRequiredCode = "VAL-LIA-OBLIGATOIRE";
    public WiringResult Evaluate(ModelSnapshot snapshot, CatalogVersion catalog)
    {
        var resourceById = snapshot.Components
            .SelectMany(component => component.Resources.Select(resource => (Component: component, Resource: resource)))
            .ToDictionary(entry => entry.Resource.Id);
        var descriptorByType = catalog.ResourceTypes.ToDictionary(descriptor => descriptor.Type, StringComparer.OrdinalIgnoreCase);
        var issues = new List<WiringIssue>();
        var implicitApplicationSettings = BuildImplicitApplicationSettings(snapshot, catalog, resourceById, descriptorByType, linkKinds);
        ValidateApplicationSettings(snapshot, resourceById, descriptorByType, implicitApplicationSettings, issues);
        var links = snapshot.Links
            .Concat(linkKinds.Values.SelectMany(handler => handler.GetImplicitLinks(snapshot, catalog)))
            .DistinctBy(link => link.Id)
            .OrderBy(link => link.SourceResourceId)
            .ThenBy(link => link.Kind)
            .ThenBy(link => link.TargetResourceId)
            .ThenBy(link => link.Id)
            .ToArray();
        var wiredLinks = new List<WiredLink>();
        var identities = new List<WiredIdentity>();
        var roleCandidates = new List<RoleCandidate>();
        var dependencies = new List<WiringDependency>();
        var dataAccessRequests = new List<DataAccessRequest>();
        AddDefaultIdentities(resourceById, descriptorByType, issues, identities);

        foreach (var link in links)
        {
            if (!linkKinds.Supports(link.Kind))
            {
                issues.Add(new WiringIssue(LinkKindUnsupportedCode, "The link kind is not supported by the current catalog.", link.Id, link.SourceResourceId));
                continue;
            }

            if (!resourceById.TryGetValue(link.SourceResourceId, out var source)
                || !resourceById.TryGetValue(link.TargetResourceId, out var target)
                || source.Component.Id != link.SourceComponentId
                || target.Component.Id != link.TargetComponentId)
            {
                issues.Add(new WiringIssue(LinkNotFoundCode, "The link source or target is missing from this project.", link.Id, link.SourceResourceId));
                continue;
            }

            if (!descriptorByType.TryGetValue(source.Resource.Type, out var sourceDescriptor)
                || !descriptorByType.TryGetValue(target.Resource.Type, out var targetDescriptor))
            {
                issues.Add(new WiringIssue(LinkKindUnsupportedCode, "The source or target type is missing from the pinned catalog.", link.Id, link.SourceResourceId));
                continue;
            }

            if (source.Resource.Id == target.Resource.Id)
            {
                issues.Add(new WiringIssue(LinkSelfCode, "A resource cannot be linked to itself.", link.Id, source.Resource.Id));
                continue;
            }

            if (source.Resource.IsExisting)
            {
                issues.Add(new WiringIssue(ExistingSourceCode, "An existing Azure resource cannot be the source of a link.", link.Id, source.Resource.Id));
                continue;
            }

            var handler = linkKinds.Get(link.Kind);
            if (link.Origin == LinkOrigin.Explicit && handler.IsImplicitOnly)
            {
                issues.Add(new WiringIssue(ImplicitOnlyCode, "This link can only be derived from another setting.", link.Id, link.SourceResourceId));
                continue;
            }

            if (!handler.Supports(sourceDescriptor, targetDescriptor))
            {
                issues.Add(new WiringIssue(LinkKindUnsupportedCode, "The pinned catalog does not allow this source and target for the link kind.", link.Id, link.SourceResourceId));
                continue;
            }

            var hasDataAccessLevelField = handler.Form.Fields.Any(field => string.Equals(field.Name, DataAccessLevelFieldName, StringComparison.Ordinal));
            if (link.DataAccessLevel is not null && !hasDataAccessLevelField)
            {
                issues.Add(new WiringIssue("VAL-LIA-DATA-LEVEL-UNSUPPORTED", "A data access level is only valid for a data access link.", link.Id, link.TargetResourceId));
                continue;
            }

            if (hasDataAccessLevelField && link.DataAccessLevel is null)
            {
                issues.Add(new WiringIssue(DataAccessLevelRequiredCode, "A data access level is required for this link.", link.Id, link.TargetResourceId));
                continue;
            }

            IDataAccessProvider? dataAccessProvider = null;
            if (hasDataAccessLevelField)
            {
                var engineNameElement = targetDescriptor.Definition.GetProperty("dataAccess").GetProperty("databaseEngine");
                var engineName = engineNameElement.ValueKind == JsonValueKind.String ? engineNameElement.GetString() : null;
                dataAccessProvider = engineName is null ? null : dataAccessProviders.FindByCatalogEngineName(engineName);
                if (dataAccessProvider is null)
                {
                    issues.Add(new WiringIssue(DataAccessUnsupportedCode, "No data access provider supports the target database engine.", link.Id, link.TargetResourceId));
                    continue;
                }
            }

            var identityRequirement = handler.GetIdentityRequirement(target.Resource.Properties);
            var issueCountBeforeIdentityResolution = issues.Count;
            var identity = ResolveIdentity(link, handler, identityRequirement, source.Component, source.Resource, sourceDescriptor,
                resourceById, descriptorByType, issues);
            if (issues.Count != issueCountBeforeIdentityResolution)
            {
                continue;
            }

            if (identity is null && identityRequirement is LinkIdentityRequirement.Required or LinkIdentityRequirement.UserAssigned)
            {
                continue;
            }

            var external = target.Component.Id != source.Component.Id || target.Resource.IsExisting;
            var evaluatedLink = identity is null
                ? link
                : link with
                {
                    IdentityKind = identity.Kind,
                    IdentityResourceId = identity.Kind == LinkIdentityKind.UserAssigned
                        ? identity.PrincipalResourceId
                        : null
                };
            wiredLinks.Add(new WiredLink(evaluatedLink, external, handler.CreatesDeploymentDependency));
            var origin = new WiringOrigin(link.Origin, link.Kind, link.Id, link.SourceResourceId, link.TargetResourceId);

            if (identity is not null)
            {
                identities.Add(new WiredIdentity(
                    source.Component.Id,
                    source.Resource.Id,
                    identity.Kind,
                    identity.AttachedIdentityResourceId,
                    [origin]));
                var requiredRoles = handler.GetRequiredRoles(link, target.Resource.Properties, targetDescriptor);
                foreach (var roleName in requiredRoles)
                {
                    var role = FindAllowedRole(targetDescriptor, roleName);
                    if (role is null || RoleAssignmentPolicy.IsForbiddenRole(role))
                    {
                        issues.Add(new WiringIssue(RoleUnsupportedCode, $"The role '{roleName}' is not assignable on this target.", link.Id, link.TargetResourceId));
                        continue;
                    }

                    var scopeResourceId = link.AccessScope == LinkAccessScope.ResourceGroup
                        ? target.Resource.ResourceGroupId
                        : target.Resource.Id;
                    roleCandidates.Add(new RoleCandidate(
                        identity.OwnerComponentId,
                        identity.PrincipalKind,
                        identity.PrincipalResourceId,
                        role,
                        link.AccessScope,
                        scopeResourceId,
                        target.Resource.Id,
                        target.Component.Id,
                        target.Resource.IsExisting,
                        handler.RoleEquivalenceKey,
                        targetDescriptor,
                        origin));
                }
            }

            if (handler.CreatesDeploymentDependency && source.Component.Id != target.Component.Id)
            {
                dependencies.Add(new WiringDependency(
                    source.Component.Id,
                    target.Component.Id,
                    link.Id,
                    link.Kind,
                    link.Origin));
            }

            if (link.DataAccessLevel is { } accessLevel)
            {
                var provider = dataAccessProvider!;

                dataAccessRequests.Add(new DataAccessRequest(
                    source.Component.Id,
                    source.Resource.Id,
                    target.Component.Id,
                    target.Resource.Id,
                    provider.Key,
                    accessLevel,
                    identity?.Kind ?? link.IdentityKind,
                    identity?.Kind == LinkIdentityKind.UserAssigned
                        ? identity.PrincipalResourceId
                        : link.IdentityResourceId,
                    provider.GetRoles(accessLevel),
                    provider.GetApplyScript(),
                    provider.GetRemoveScript(),
                    origin));
            }
        }

        foreach (var component in snapshot.Components)
        {
            foreach (var resource in component.Resources)
            {
                if (!descriptorByType.TryGetValue(resource.Type, out var descriptor))
                {
                    continue;
                }

                foreach (var handler in linkKinds.Values)
                {
                    var cardinality = handler.GetCardinality(descriptor);
                    var count = wiredLinks.Count(wired =>
                        wired.Link.SourceResourceId == resource.Id && wired.Link.Kind == handler.Key);
                    if (count < cardinality.Minimum)
                    {
                        issues.Add(new WiringIssue(LinkRequiredCode,
                            $"The resource requires at least {cardinality.Minimum} {handler.Key} link(s).", null, resource.Id));
                    }
                    else if (cardinality.Maximum is { } maximum && count > maximum)
                    {
                        issues.Add(new WiringIssue(LinkCardinalityCode,
                            $"The resource allows at most {maximum} {handler.Key} link(s).", null, resource.Id));
                    }
                }
            }
        }

        foreach (var attachedIdentities in identities
            .Where(identity => identity.Kind == LinkIdentityKind.UserAssigned && identity.AttachedIdentityResourceId is not null)
            .GroupBy(identity => (identity.ComponentId, identity.ResourceId)))
        {
            if (!resourceById.TryGetValue(attachedIdentities.Key.ResourceId, out var source)
                || !descriptorByType.TryGetValue(source.Resource.Type, out var sourceDescriptor))
            {
                continue;
            }

            var maximum = sourceDescriptor.Definition.GetProperty("identities").GetProperty("userAssignedMax").GetInt32();
            var count = attachedIdentities.Select(identity => identity.AttachedIdentityResourceId).Distinct().Count();
            if (count > maximum)
            {
                issues.Add(new WiringIssue(IdentityCapacityCode,
                    $"The source allows at most {maximum} user-assigned identities.",
                    null,
                    source.Resource.Id));
            }
        }

        var (roleAssignments, roleEquivalences) = BuildRoleAssignments(roleCandidates);
        return new WiringResult(
            wiredLinks.OrderBy(link => link.Link.SourceResourceId).ThenBy(link => link.Link.Kind)
                .ThenBy(link => link.Link.TargetResourceId).ThenBy(link => link.Link.Id).ToArray(),
            MergeIdentities(identities),
            roleAssignments,
            roleEquivalences,
            dependencies.OrderBy(dependency => dependency.SourceComponentId)
                .ThenBy(dependency => dependency.TargetComponentId).ThenBy(dependency => dependency.LinkId).ToArray(),
            dataAccessRequests.OrderBy(request => request.OwnerComponentId)
                .ThenBy(request => request.SourceResourceId).ThenBy(request => request.TargetResourceId).ToArray(),
            issues.OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.LinkId).ThenBy(issue => issue.ResourceId).ToArray())
        {
            ImplicitApplicationSettings = implicitApplicationSettings
        };
    }

    private static ImplicitApplicationSetting[] BuildImplicitApplicationSettings(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        Dictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resources,
        Dictionary<string, CatalogResourceTypeDescriptor> descriptors,
        LinkKindRegistry linkKinds)
    {
        var settings = new List<ImplicitApplicationSetting>();
        var linksByKind = snapshot.Links
            .GroupBy(link => link.Kind)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LinkSnapshot>)group.OrderBy(link => link.Id).ToArray());
        foreach (var handler in linkKinds.Values)
        {
            if (linksByKind.TryGetValue(handler.Key, out var links))
            {
                settings.AddRange(handler.GetImplicitApplicationSettings(snapshot, catalog, links));
            }
        }

        foreach (var application in resources.Values
            .Where(value => value.Resource.DefaultIdentityResourceId is not null)
            .OrderBy(value => value.Resource.Id))
        {
            if (!resources.TryGetValue(application.Resource.DefaultIdentityResourceId!.Value, out var identity)
                || !descriptors.TryGetValue(identity.Resource.Type, out var identityDescriptor)
                || FindOutput(identityDescriptor, "clientId") is not { } clientId)
            {
                continue;
            }

            settings.Add(new ImplicitApplicationSetting(
                LinkIdFactory.Create($"implicitAppSetting:ApplicationIdentity:{ApplicationSettingNameRules.AzureClientId}", application.Resource.Id, identity.Resource.Id),
                application.Resource.Id,
                ApplicationSettingNameRules.AzureClientId,
                identity.Resource.Id,
                "clientId",
                null,
                clientId.GetProperty("sensitive").GetBoolean(),
                "ApplicationIdentity"));
        }

        return settings
            .DistinctBy(setting => (setting.ApplicationResourceId, setting.Name, setting.SourceResourceId, setting.OutputName))
            .OrderBy(setting => setting.ApplicationResourceId)
            .ThenBy(setting => setting.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddDefaultIdentities(
        Dictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resources,
        Dictionary<string, CatalogResourceTypeDescriptor> descriptors,
        List<WiringIssue> issues,
        List<WiredIdentity> identities)
    {
        foreach (var application in resources.Values
            .Where(value => value.Resource.DefaultIdentityResourceId is not null)
            .OrderBy(value => value.Resource.Id))
        {
            var identityId = application.Resource.DefaultIdentityResourceId!.Value;
            if (!descriptors.TryGetValue(application.Resource.Type, out var applicationDescriptor)
                || !applicationDescriptor.Definition.TryGetProperty("identities", out var applicationIdentities)
                || applicationIdentities.GetProperty("userAssignedMax").GetInt32() == 0)
            {
                issues.Add(new WiringIssue(IdentityUnsupportedCode,
                    "The application type does not support a default user-assigned identity.", null, application.Resource.Id));
                continue;
            }

            if (!resources.TryGetValue(identityId, out var identity)
                || !string.Equals(identity.Resource.Type, "UserAssignedIdentity", StringComparison.Ordinal))
            {
                issues.Add(new WiringIssue(IdentityUnsupportedCode,
                    "The selected default identity is unavailable or is not a user-assigned identity.", null, application.Resource.Id));
                continue;
            }

            if (identity.Component.Id != application.Component.Id)
            {
                issues.Add(new WiringIssue(SharedIdentityCode,
                    "A user-assigned identity cannot be shared across deployment components.", null, application.Resource.Id));
                continue;
            }

            identities.Add(new WiredIdentity(
                application.Component.Id,
                application.Resource.Id,
                LinkIdentityKind.UserAssigned,
                identityId,
                []));
        }
    }

    private static void ValidateApplicationSettings(
        ModelSnapshot snapshot,
        Dictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resources,
        Dictionary<string, CatalogResourceTypeDescriptor> descriptors,
        IReadOnlyList<ImplicitApplicationSetting> implicitSettings,
        List<WiringIssue> issues)
    {
        var implicitNames = implicitSettings
            .GroupBy(setting => setting.ApplicationResourceId)
            .ToDictionary(group => group.Key,
                group => group.Select(setting => setting.Name).ToArray());

        foreach (var setting in snapshot.AppSettings)
        {
            if (implicitNames.TryGetValue(setting.ApplicationResourceId, out var names)
                && names.Any(name => ApplicationSettingNameRules.ConflictsWithImplicit(setting.Name, name)))
            {
                issues.Add(new WiringIssue("VAL-PAR-IMPLICITE-CONFLIT",
                    $"The explicit setting '{setting.Name}' conflicts with a setting derived from a resource link.",
                    null,
                    setting.ApplicationResourceId));
            }

            if (setting.SourceKind != InfraFlowSculptor.Domain.ComponentAggregate.AppSettingSourceKind.ResourceOutput
                || setting.SourceResourceId is not { } sourceId)
            {
                continue;
            }

            if (!resources.TryGetValue(sourceId, out var source)
                || !descriptors.TryGetValue(source.Resource.Type, out var sourceDescriptor)
                || FindOutput(sourceDescriptor, setting.OutputName) is not { } output)
            {
                issues.Add(new WiringIssue("VAL-PAR-OUTPUT-UNKNOWN",
                    $"The output '{setting.OutputName}' is not available on the selected resource.",
                    null,
                    setting.ApplicationResourceId));
                continue;
            }

            if (output.GetProperty("sensitive").GetBoolean())
            {
                issues.Add(new WiringIssue("VAL-PAR-04",
                    $"The output '{setting.OutputName}' is sensitive and cannot be assigned as an environment variable.",
                    null,
                    setting.ApplicationResourceId));
            }
        }
    }

    private static JsonElement? FindOutput(CatalogResourceTypeDescriptor descriptor, string? name)
    {
        foreach (var output in descriptor.Definition.GetProperty("outputs").EnumerateArray())
        {
            if (string.Equals(output.GetProperty("name").GetString(), name, StringComparison.Ordinal))
            {
                return output;
            }
        }

        return null;
    }

    private static EffectiveIdentity? ResolveIdentity(
        LinkSnapshot link,
        ILinkKindHandler handler,
        LinkIdentityRequirement identityRequirement,
        ComponentSnapshot sourceComponent,
        ResourceSnapshot sourceResource,
        CatalogResourceTypeDescriptor sourceDescriptor,
        Dictionary<Guid, (ComponentSnapshot Component, ResourceSnapshot Resource)> resources,
        Dictionary<string, CatalogResourceTypeDescriptor> descriptors,
        List<WiringIssue> issues)
    {
        var kind = link.IdentityKind;
        var identityResourceId = link.IdentityResourceId;
        var sourceActsAsIdentity = kind == LinkIdentityKind.None && handler.SourceActsAsIdentity(sourceDescriptor);
        if (sourceActsAsIdentity)
        {
            kind = LinkIdentityKind.UserAssigned;
            identityResourceId = sourceResource.Id;
        }

        if (identityRequirement == LinkIdentityRequirement.None)
        {
            if (kind != LinkIdentityKind.None)
            {
                issues.Add(new WiringIssue(IdentityUnsupportedCode, "This link kind does not use an identity.", link.Id, sourceResource.Id));
                return null;
            }

            return null;
        }

        if (identityRequirement is LinkIdentityRequirement.Required or LinkIdentityRequirement.UserAssigned
            && kind == LinkIdentityKind.None)
        {
            issues.Add(new WiringIssue(IdentityRequiredCode, "This link requires a managed identity.", link.Id, sourceResource.Id));
            return null;
        }

        if (identityRequirement == LinkIdentityRequirement.UserAssigned && kind != LinkIdentityKind.UserAssigned)
        {
            issues.Add(new WiringIssue(IdentityUnsupportedCode, "This link requires a user-assigned identity.", link.Id, sourceResource.Id));
            return null;
        }

        if (identityRequirement == LinkIdentityRequirement.Optional && kind == LinkIdentityKind.None)
        {
            return null;
        }

        if (kind == LinkIdentityKind.System)
        {
            if (!sourceDescriptor.Definition.GetProperty("identities").GetProperty("system").GetBoolean())
            {
                issues.Add(new WiringIssue(IdentityUnsupportedCode, "The source type does not support a system-assigned identity.", link.Id, sourceResource.Id));
                return null;
            }

            return new EffectiveIdentity(sourceComponent.Id, sourceResource.Id, LinkIdentityKind.System,
                RolePrincipalKind.SystemAssigned, null);
        }

        if (kind != LinkIdentityKind.UserAssigned || identityResourceId is null
            || (!sourceActsAsIdentity && sourceDescriptor.Definition.GetProperty("identities").GetProperty("userAssignedMax").GetInt32() == 0)
            || !resources.TryGetValue(identityResourceId.Value, out var identityResource)
            || !descriptors.TryGetValue(identityResource.Resource.Type, out var identityDescriptor)
            || !string.Equals(identityDescriptor.Type, "UserAssignedIdentity", StringComparison.Ordinal)
            || identityResource.Component.Id != sourceComponent.Id)
        {
            var code = identityResourceId is { } assignedId
                && resources.TryGetValue(assignedId, out var assignedResource)
                && assignedResource.Component.Id != sourceComponent.Id
                    ? SharedIdentityCode
                    : IdentityUnsupportedCode;
            issues.Add(new WiringIssue(code, code == SharedIdentityCode
                ? "A user-assigned identity cannot be shared across deployment components."
                : "The selected user-assigned identity is unavailable or unsupported by the source.", link.Id, identityResourceId));
            return null;
        }

        return new EffectiveIdentity(identityResource.Component.Id, identityResource.Resource.Id,
            LinkIdentityKind.UserAssigned, RolePrincipalKind.UserAssigned,
            sourceActsAsIdentity ? null : identityResource.Resource.Id);
    }

    private static (IReadOnlyList<RoleAssignmentSnapshot>, IReadOnlyList<WiringRoleEquivalence>) BuildRoleAssignments(
        IReadOnlyList<RoleCandidate> candidates)
    {
        var retained = new List<RoleCandidate>();
        var equivalences = new List<WiringRoleEquivalence>();
        foreach (var candidate in candidates)
        {
            if (candidate.Origin.LinkOrigin == LinkOrigin.Implicit && candidate.RoleEquivalenceKey is { } key)
            {
                var equivalentRoles = ReadEquivalentRoles(candidate.TargetDescriptor, key);
                var existing = candidates.FirstOrDefault(other =>
                    other.Origin.LinkOrigin == LinkOrigin.Explicit
                    && SamePrincipalAndScope(candidate, other)
                    && !string.Equals(candidate.RoleName, other.RoleName, StringComparison.OrdinalIgnoreCase)
                    && equivalentRoles.Contains(other.RoleName, StringComparer.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    equivalences.Add(new WiringRoleEquivalence(
                        candidate.TargetResourceId,
                        candidate.RoleName,
                        existing.RoleName,
                        candidate.Origin,
                        existing.Origin));
                    continue;
                }
            }

            retained.Add(candidate);
        }

        var assignments = retained
            .GroupBy(candidate => new RoleAssignmentKey(
                candidate.PrincipalKind,
                candidate.PrincipalResourceId,
                candidate.RoleName,
                candidate.ScopeKind,
                candidate.ScopeResourceId))
            .Select(group =>
            {
                var first = group.First();
                var origins = OrderOrigins(group.Select(candidate => candidate.Origin));
                return new RoleAssignmentSnapshot(
                    first.OwnerComponentId,
                    first.PrincipalKind,
                    first.PrincipalResourceId,
                    first.RoleName,
                    first.ScopeKind,
                    first.ScopeResourceId,
                    first.TargetComponentId,
                    group.Any(candidate => candidate.IsTargetExternal)
                        || first.OwnerComponentId != first.TargetComponentId,
                    CreateRoleAssignmentName(first),
                    group.Select(candidate => candidate.TargetResourceId).Distinct().Order().ToArray(),
                    origins);
            })
            .OrderBy(assignment => assignment.PrincipalResourceId)
            .ThenBy(assignment => assignment.RoleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(assignment => assignment.ScopeResourceId)
            .ToArray();

        var orderedEquivalences = equivalences
            .OrderBy(equivalence => equivalence.TargetResourceId)
            .ThenBy(equivalence => equivalence.RequestedRole, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return (assignments, orderedEquivalences);
    }

    private static WiredIdentity[] MergeIdentities(IEnumerable<WiredIdentity> identities) => identities
        .GroupBy(identity => new IdentityKey(identity.ComponentId, identity.ResourceId, identity.Kind, identity.AttachedIdentityResourceId))
        .Select(group => new WiredIdentity(
            group.Key.ComponentId,
            group.Key.ResourceId,
            group.Key.Kind,
            group.First().AttachedIdentityResourceId,
            OrderOrigins(group.SelectMany(identity => identity.Origins))))
        .OrderBy(identity => identity.ComponentId)
        .ThenBy(identity => identity.ResourceId)
        .ThenBy(identity => identity.Kind)
        .ToArray();

    private static WiringOrigin[] OrderOrigins(IEnumerable<WiringOrigin> origins) => origins
        .Distinct()
        .OrderBy(origin => origin.LinkOrigin)
        .ThenBy(origin => origin.LinkKind)
        .ThenBy(origin => origin.LinkId)
        .ToArray();

    private static string? FindAllowedRole(CatalogResourceTypeDescriptor target, string desiredRole) =>
        target.Definition.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()!)
            .FirstOrDefault(role => string.Equals(role, desiredRole, StringComparison.OrdinalIgnoreCase));

    private static string[] ReadEquivalentRoles(CatalogResourceTypeDescriptor target, string key)
    {
        var equivalences = target.Definition.GetProperty("roleEquivalences");
        foreach (var property in equivalences.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.EnumerateArray().Select(value => value.GetString()!).ToArray();
            }
        }

        return [];
    }

    private static bool SamePrincipalAndScope(RoleCandidate left, RoleCandidate right) =>
        left.PrincipalKind == right.PrincipalKind
        && left.PrincipalResourceId == right.PrincipalResourceId
        && left.ScopeKind == right.ScopeKind
        && left.ScopeResourceId == right.ScopeResourceId;

    private static string CreateRoleAssignmentName(RoleCandidate candidate)
    {
        var input = Encoding.UTF8.GetBytes(string.Join('|',
            candidate.PrincipalKind.ToString(),
            candidate.PrincipalResourceId.ToString("N"),
            candidate.RoleName.ToUpperInvariant(),
            candidate.ScopeKind.ToString(),
            candidate.ScopeResourceId.ToString("N")));
        var bytes = SHA256.HashData(input)[..16];
        return new Guid(bytes).ToString("D");
    }

    private sealed record EffectiveIdentity(
        Guid OwnerComponentId,
        Guid PrincipalResourceId,
        LinkIdentityKind Kind,
        RolePrincipalKind PrincipalKind,
        Guid? AttachedIdentityResourceId);

    private sealed record RoleCandidate(
        Guid OwnerComponentId,
        RolePrincipalKind PrincipalKind,
        Guid PrincipalResourceId,
        string RoleName,
        LinkAccessScope ScopeKind,
        Guid ScopeResourceId,
        Guid TargetResourceId,
        Guid TargetComponentId,
        bool IsTargetExternal,
        string? RoleEquivalenceKey,
        CatalogResourceTypeDescriptor TargetDescriptor,
        WiringOrigin Origin);

    private sealed record RoleAssignmentKey(
        RolePrincipalKind PrincipalKind,
        Guid PrincipalResourceId,
        string RoleName,
        LinkAccessScope ScopeKind,
        Guid ScopeResourceId);

    private sealed record IdentityKey(Guid ComponentId, Guid ResourceId, LinkIdentityKind Kind, Guid? AttachedIdentityResourceId);
}
