using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.AzureDevOps;

public sealed class AzureDevOpsPipelineEmitter(StepTranslatorRegistry stepTranslators) : IPipelineEmitter
{
    private const string DefaultBranch = "main";
    private const string InfrastructureDirectoryName = "infra";
    private const string PipelineDirectoryName = "pipelines";
    private const string ApplicationDirectoryName = "apps";

    public CiPlatform Key => CiPlatform.AzureDevOps;

    public IReadOnlyList<GeneratedFile> Emit(DeploymentPlan plan, CatalogVersion catalog)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(catalog);

        ArgumentNullException.ThrowIfNull(stepTranslators);

        var translator = stepTranslators.Get(Key);
        var files = new List<GeneratedFile>(AzureDevOpsTemplateResources.ReadAll(plan.Project.Code, plan.Project.Name));
        foreach (var component in plan.Components.OrderBy(component => component.ComponentCode, StringComparer.Ordinal))
        {
            files.AddRange(EmitInfrastructurePipelines(plan, component));
            files.AddRange(component.Applications.OrderBy(application => application.Name, StringComparer.Ordinal)
                .SelectMany(application => EmitApplicationPipelines(plan, component, application, catalog, translator)));
        }

        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<GeneratedFile> EmitInfrastructurePipelines(DeploymentPlan plan, ComponentPlan component)
    {
        var root = $"{component.ComponentCode}/{InfrastructureDirectoryName}/{PipelineDirectoryName}";
        yield return new GeneratedFile($"{root}/pr.yml", WriteInfrastructurePullRequest(plan, component));
        yield return new GeneratedFile($"{root}/ci.yml", WriteInfrastructureCi(plan, component));
        yield return new GeneratedFile($"{root}/release.yml", WriteInfrastructureRelease(plan, component));
    }

    private static IEnumerable<GeneratedFile> EmitApplicationPipelines(
        DeploymentPlan plan,
        ComponentPlan component,
        ApplicationPlan application,
        CatalogVersion catalog,
        IStepTranslator translator)
    {
        ValidateApplicationPaths(application);
        var root = $"{component.ComponentCode}/{ApplicationDirectoryName}/{application.Name}/{PipelineDirectoryName}";
        var steps = TranslateSteps(application, catalog, translator);
        yield return new GeneratedFile($"{root}/pr.yml", WriteApplicationPullRequest(plan, component, application, steps));
        yield return new GeneratedFile($"{root}/ci.yml", WriteApplicationCi(plan, component, application, steps));
        yield return new GeneratedFile($"{root}/release.yml", WriteApplicationRelease(plan, component, application));
    }

    private static string WriteInfrastructurePullRequest(DeploymentPlan plan, ComponentPlan component)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger: none");
        writer.WriteLine("# Azure Repos Git déclenche cette validation par une Build Validation branch policy, configurée par le kit d’installation.");
        writer.WriteLine("pr: none");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/infra-pr.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        return writer.Build();
    }

    private static string WriteInfrastructureCi(DeploymentPlan plan, ComponentPlan component)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger:");
        writer.WriteLine("  branches:");
        writer.WriteLine("    include:");
        writer.WriteLine($"    - {DefaultBranch}");
        writer.WriteLine("  paths:");
        writer.WriteLine("    include:");
        writer.WriteLine($"    - {component.ComponentCode}/infra/**");
        writer.WriteLine("    - .ifs/**");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/infra-ci.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        return writer.Build();
    }

    private static string WriteInfrastructureRelease(DeploymentPlan plan, ComponentPlan component)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger: none");
        var systemAssignedTargets = component.Targets
            .Where(target => target.Release.DataAccess.Any(access =>
                string.Equals(access.Principal.Kind, "systemAssigned", StringComparison.Ordinal)))
            .Select(target => target.Target.Code)
            .ToHashSet(StringComparer.Ordinal);
        WriteSystemAssignedSqlClientParameters(writer, component.Targets, systemAssignedTargets);
        WriteCiPipelineResource(writer, plan.Project.Code, component.ComponentCode, "infra", "ci");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/infra-release.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        writer.WriteLine($"    defaultBranch: {DefaultBranch}");
        WriteSecretWrites(writer, component);
        WriteTargets(writer, component.Targets.Select(target => target.Target).ToArray(), systemAssignedTargets);
        return writer.Build();
    }

    private static string WriteApplicationPullRequest(
        DeploymentPlan plan,
        ComponentPlan component,
        ApplicationPlan application,
        IReadOnlyList<PipelineStepTranslation> steps)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger: none");
        writer.WriteLine("# Azure Repos Git déclenche cette validation par une Build Validation branch policy, configurée par le kit d’installation.");
        writer.WriteLine("pr: none");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/app-pr.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {Quote(plan.Project.Code)}");
        writer.WriteLine($"    component: {Quote(component.ComponentCode)}");
        writer.WriteLine($"    application: {Quote(application.Name)}");
        writer.WriteLine($"    buildContext: {Quote(application.BuildContextPath)}");
        writer.WriteLine($"    dockerfile: {Quote(application.DockerfilePath)}");
        WriteDependencyCachePath(writer, steps);
        WriteApplicationStepLists(writer, application, steps);
        return writer.Build();
    }

    private static string WriteApplicationCi(
        DeploymentPlan plan,
        ComponentPlan component,
        ApplicationPlan application,
        IReadOnlyList<PipelineStepTranslation> steps)
    {
        var registryTarget = FindBuildRegistryTarget(application);
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger:");
        writer.WriteLine("  branches:");
        writer.WriteLine("    include:");
        writer.WriteLine($"    - {DefaultBranch}");
        writer.WriteLine("  paths:");
        writer.WriteLine("    include:");
        var codeSourcePath = application.CodeSourcePath + "/**";
        var serializedCodeSourcePath = QuoteYamlScalarIfNeeded(codeSourcePath);
        writer.WriteLine($"    - {serializedCodeSourcePath}");
        writer.WriteLine($"    - {component.ComponentCode}/apps/{application.Name}/**");
        writer.WriteLine("    - .ifs/templates/**");
        writer.WriteLine("    - .ifs/pins.json");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/app-ci.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {Quote(plan.Project.Code)}");
        writer.WriteLine($"    component: {Quote(component.ComponentCode)}");
        writer.WriteLine($"    application: {Quote(application.Name)}");
        writer.WriteLine($"    registryServiceConnection: {Quote($"ifs-{plan.Project.Code}-{registryTarget}-app")}");
        writer.WriteLine($"    buildContext: {Quote(application.BuildContextPath)}");
        writer.WriteLine($"    dockerfile: {Quote(application.DockerfilePath)}");
        WriteDependencyCachePath(writer, steps);
        WriteApplicationStepLists(writer, application, steps);
        return writer.Build();
    }

    private static string WriteApplicationRelease(DeploymentPlan plan, ComponentPlan component, ApplicationPlan application)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger: none");
        WriteCiPipelineResource(writer, plan.Project.Code, component.ComponentCode, application.Name, "build");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/app-release.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        writer.WriteLine($"    application: {application.Name}");
        WriteTargets(writer, application.Targets.Select(target => component.Targets
            .Single(componentTarget => string.Equals(componentTarget.Target.Code, target.TargetCode, StringComparison.Ordinal))
            .Target).ToArray());
        return writer.Build();
    }

    private static YamlWriter CreatePipelineHeader(string projectCode)
    {
        var writer = new YamlWriter();
        writer.WriteLine($"# Généré par InfraFlowSculptor — projet {projectCode}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.");
        return writer;
    }

    private static void WriteCiPipelineResource(
        YamlWriter writer,
        string projectCode,
        string componentCode,
        string pipelineKind,
        string pipelineAlias)
    {
        writer.WriteLine("resources:");
        writer.WriteLine("  pipelines:");
        writer.WriteLine($"  - pipeline: {pipelineAlias}");
        writer.WriteLine($"    source: '{projectCode} · {componentCode} · {pipelineKind} · CI'");
        writer.WriteLine("    trigger:");
        writer.WriteLine("      branches:");
        writer.WriteLine("        include:");
        writer.WriteLine($"        - {DefaultBranch}");
    }

    private static void WriteSecretWrites(YamlWriter writer, ComponentPlan component)
    {
        var secrets = component.Targets.SelectMany(target => target.Release.SecretWrites)
            .GroupBy(secret => secret.Variable, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(secret => secret.Variable, StringComparer.Ordinal)
            .ToArray();
        if (secrets.Length == 0)
        {
            writer.WriteLine("    secretWrites: []");
            return;
        }

        writer.WriteLine("    secretWrites:");
        foreach (var secret in secrets)
        {
            writer.WriteLine($"    - name: {secret.Variable}");
            writer.WriteLine($"      value: $({secret.Variable})");
        }
    }

    private static void WriteTargets(
        YamlWriter writer,
        TargetInfo[] targets,
        HashSet<string>? systemAssignedTargets = null)
    {
        writer.WriteLine("    targets:");
        for (var index = 0; index < targets.Length; index++)
        {
            var target = targets[index];
            writer.WriteLine($"    - name: {target.Code}");
            writer.WriteLine($"      protected: {target.IsProtected.ToString().ToLowerInvariant()}");
            if (systemAssignedTargets?.Contains(target.Code) == true)
            {
                writer.WriteLine($"      systemAssignedSqlClientId: ${{{{ parameters.systemAssignedSqlClientId{TargetParameterSuffix(target.Code)} }}}}");
            }
            if (index == 0)
            {
                writer.WriteLine("      dependsOn: []");
                continue;
            }

            writer.WriteLine("      dependsOn:");
            writer.WriteLine($"      - Deploy_{targets[index - 1].Code}");
        }
    }

    private static void WriteSystemAssignedSqlClientParameters(
        YamlWriter writer,
        IReadOnlyList<TargetPlan> targets,
        HashSet<string> systemAssignedTargets)
    {
        var selected = targets.Where(target => systemAssignedTargets.Contains(target.Target.Code)).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        writer.WriteLine("parameters:");
        foreach (var target in selected)
        {
            var targetName = target.Target.Code.ToUpperInvariant();
            writer.WriteLine($"- name: systemAssignedSqlClientId{TargetParameterSuffix(target.Target.Code)}");
            writer.WriteLine($"  displayName: Client ID SQL de l'identité système {targetName} (optionnel si la connexion lit Entra)");
            writer.WriteLine("  type: string");
            writer.WriteLine("  default: ''");
        }
    }

    private static string TargetParameterSuffix(string targetCode) =>
        string.Concat(targetCode.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));

    private static string FindBuildRegistryTarget(ApplicationPlan application)
    {
        var targets = application.Targets.Select(target => target.BuildRegistryTarget)
            .Where(target => target is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (targets.Length != 1)
        {
            throw new InvalidOperationException($"Application '{application.Name}' must resolve one build registry target.");
        }

        return targets[0]!;
    }

    private static PipelineStepTranslation[] TranslateSteps(
        ApplicationPlan application,
        CatalogVersion catalog,
        IStepTranslator translator) => application.Steps
        .Where(step => step.Enabled)
        .OrderBy(step => step.Position)
        .ThenBy(step => step.Name, StringComparer.Ordinal)
        .Select(step =>
        {
            var descriptor = catalog.PipelineSteps.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, step.Name, StringComparison.Ordinal));
            if (descriptor is null)
            {
                throw new InvalidOperationException($"Enabled application step '{step.Name}' is absent from catalog '{catalog.Version}'.");
            }

            return translator.Translate(descriptor, step);
        })
        .ToArray();

    private static void WriteApplicationStepLists(
        YamlWriter writer,
        ApplicationPlan application,
        IReadOnlyList<PipelineStepTranslation> steps)
    {
        WriteStepList("stepsBeforeBuild", ApplicationStepPosition.BeforeBuild);
        WriteStepList("stepsAfterBuild", ApplicationStepPosition.AfterBuild);

        void WriteStepList(string parameterName, ApplicationStepPosition position)
        {
            var selected = steps.Where(step => step.Position == position).ToArray();
            if (selected.Length == 0)
            {
                writer.WriteLine($"    {parameterName}: []");
                return;
            }

            writer.WriteLine($"    {parameterName}:");
            foreach (var step in selected)
            {
                writer.WriteLine($"    - template: /{step.Template}");
                writer.WriteLine("      parameters:");
                writer.WriteLine($"        application: {Quote(application.Name)}");
                foreach (var parameter in step.Parameters)
                {
                    writer.WriteLine($"        {parameter.Name}: {Quote(parameter.Value)}");
                }

                writer.WriteLine($"        blocking: {step.Blocking.ToString().ToLowerInvariant()}");
            }
        }
    }

    private static void WriteDependencyCachePath(
        YamlWriter writer,
        IReadOnlyList<PipelineStepTranslation> steps)
    {
        var enabled = steps.Any(step => string.Equals(
            step.Template,
            ".ifs/templates/steps/DependencyCache.yml",
            StringComparison.Ordinal));
        var path = enabled ? "$(Pipeline.Workspace)/.ifs-buildx-cache" : string.Empty;
        writer.WriteLine($"    dependencyCachePath: {Quote(path)}");
    }

    private static void ValidateApplicationPaths(ApplicationPlan application)
    {
        foreach (var (name, path) in new[]
        {
            (nameof(application.CodeSourcePath), application.CodeSourcePath),
            (nameof(application.BuildContextPath), application.BuildContextPath),
            (nameof(application.DockerfilePath), application.DockerfilePath)
        })
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Contains("$(", StringComparison.Ordinal)
                || path.Contains("$" + "{{", StringComparison.Ordinal)
                || path.Contains('\r')
                || path.Contains('\n'))
            {
                throw new InvalidOperationException($"Application {name} must be a repository path without Azure Pipelines expressions.");
            }
        }
    }

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string QuoteYamlScalarIfNeeded(string value)
    {
        if (value.Length == 0
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1])
            || value.Contains('#', StringComparison.Ordinal)
            || value.Contains(": ", StringComparison.Ordinal)
            || value.EndsWith(':')
            || value[0] is '[' or '{' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`'
            || ((value[0] is '-' or '?' or ':') && (value.Length == 1 || char.IsWhiteSpace(value[1])))
        )
        {
            return Quote(value);
        }

        return value;
    }

}
