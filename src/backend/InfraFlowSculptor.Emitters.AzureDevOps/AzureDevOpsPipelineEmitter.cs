using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.AzureDevOps;

public sealed class AzureDevOpsPipelineEmitter : IPipelineEmitter
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

        var files = new List<GeneratedFile>(AzureDevOpsTemplateResources.ReadAll());
        foreach (var component in plan.Components.OrderBy(component => component.ComponentCode, StringComparer.Ordinal))
        {
            files.AddRange(EmitInfrastructurePipelines(plan, component));
            files.AddRange(component.Applications.OrderBy(application => application.Name, StringComparer.Ordinal)
                .SelectMany(application => EmitApplicationPipelines(plan, component, application)));
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
        ApplicationPlan application)
    {
        var root = $"{component.ComponentCode}/{ApplicationDirectoryName}/{application.Name}/{PipelineDirectoryName}";
        yield return new GeneratedFile($"{root}/pr.yml", WriteApplicationPullRequest(plan, component, application));
        yield return new GeneratedFile($"{root}/ci.yml", WriteApplicationCi(plan, component, application));
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
        WriteCiPipelineResource(writer, plan.Project.Code, component.ComponentCode, "infra", "ci");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/infra-release.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        writer.WriteLine($"    defaultBranch: {DefaultBranch}");
        WriteSecretWrites(writer, component);
        WriteTargets(writer, component.Targets.Select(target => target.Target).ToArray());
        return writer.Build();
    }

    private static string WriteApplicationPullRequest(DeploymentPlan plan, ComponentPlan component, ApplicationPlan application)
    {
        var writer = CreatePipelineHeader(plan.Project.Code);
        writer.WriteLine("name: $(Date:yyyyMMdd).$(Rev:r)");
        writer.WriteLine("trigger: none");
        writer.WriteLine("# Azure Repos Git déclenche cette validation par une Build Validation branch policy, configurée par le kit d’installation.");
        writer.WriteLine("pr: none");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/app-pr.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        writer.WriteLine($"    application: {application.Name}");
        writer.WriteLine($"    buildContext: {application.BuildContextPath}");
        writer.WriteLine($"    dockerfile: {application.DockerfilePath}");
        return writer.Build();
    }

    private static string WriteApplicationCi(DeploymentPlan plan, ComponentPlan component, ApplicationPlan application)
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
        writer.WriteLine($"    - {application.CodeSourcePath}/**");
        writer.WriteLine($"    - {component.ComponentCode}/apps/{application.Name}/**");
        writer.WriteLine("    - .ifs/templates/**");
        writer.WriteLine("    - .ifs/pins.json");
        writer.WriteLine("extends:");
        writer.WriteLine("  template: /.ifs/templates/app-ci.yml");
        writer.WriteLine("  parameters:");
        writer.WriteLine($"    project: {plan.Project.Code}");
        writer.WriteLine($"    component: {component.ComponentCode}");
        writer.WriteLine($"    application: {application.Name}");
        writer.WriteLine($"    registryServiceConnection: ifs-{plan.Project.Code}-{registryTarget}-app");
        writer.WriteLine($"    buildContext: {application.BuildContextPath}");
        writer.WriteLine($"    dockerfile: {application.DockerfilePath}");
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

    private static void WriteTargets(YamlWriter writer, TargetInfo[] targets)
    {
        writer.WriteLine("    targets:");
        for (var index = 0; index < targets.Length; index++)
        {
            var target = targets[index];
            writer.WriteLine($"    - name: {target.Code}");
            writer.WriteLine($"      protected: {target.IsProtected.ToString().ToLowerInvariant()}");
            if (index == 0)
            {
                writer.WriteLine("      dependsOn: []");
                continue;
            }

            writer.WriteLine("      dependsOn:");
            writer.WriteLine($"      - Deploy_{targets[index - 1].Code}");
        }
    }

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

}
