namespace InfraFlowSculptor.Emitters.AzureDevOps;

internal static class AzureDevOpsTemplateResources
{
    private const string ResourcePrefix = "InfraFlowSculptor.Emitters.AzureDevOps.Templates.";
    private const string ReferenceProjectCode = "shop";

    private static readonly IReadOnlyDictionary<string, string> PathsByResource =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["infra-pr.yml"] = ".ifs/templates/infra-pr.yml",
            ["infra-ci.yml"] = ".ifs/templates/infra-ci.yml",
            ["infra-release.yml"] = ".ifs/templates/infra-release.yml",
            ["infra-target-stages.yml"] = ".ifs/templates/infra-target-stages.yml",
            ["app-pr.yml"] = ".ifs/templates/app-pr.yml",
            ["app-ci.yml"] = ".ifs/templates/app-ci.yml",
            ["app-release.yml"] = ".ifs/templates/app-release.yml",
            ["app-target-stage.yml"] = ".ifs/templates/app-target-stage.yml",
            ["schemas.ifs-app-report.schema.json"] = ".ifs/templates/schemas/ifs-app-report.schema.json",
            ["schemas.ifs-preview.schema.json"] = ".ifs/templates/schemas/ifs-preview.schema.json",
            ["schemas.ifs-report.schema.json"] = ".ifs/templates/schemas/ifs-report.schema.json",
            ["schemas.ifs-report.v2.schema.json"] = ".ifs/templates/schemas/ifs-report.v2.schema.json",
            ["schemas.operation-journal.schema.json"] = ".ifs/templates/schemas/operation-journal.schema.json",
            ["schemas.release-data.schema.json"] = ".ifs/templates/schemas/release-data.schema.json",
            ["scripts.Build-IfsImage.ps1"] = ".ifs/templates/scripts/Build-IfsImage.ps1",
            ["scripts.IfsLeaseRenewal.ps1"] = ".ifs/templates/scripts/IfsLeaseRenewal.ps1",
            ["scripts.IfsRelease.psm1"] = ".ifs/templates/scripts/IfsRelease.psm1",
            ["scripts.Install-IfsTrivy.ps1"] = ".ifs/templates/scripts/Install-IfsTrivy.ps1",
            ["scripts.Invoke-IfsImageScan.ps1"] = ".ifs/templates/scripts/Invoke-IfsImageScan.ps1",
            ["scripts.Invoke-IfsInfraPreview.ps1"] = ".ifs/templates/scripts/Invoke-IfsInfraPreview.ps1",
            ["scripts.Invoke-IfsInfraDeploy.ps1"] = ".ifs/templates/scripts/Invoke-IfsInfraDeploy.ps1",
            ["scripts.Invoke-IfsAppDeploy.ps1"] = ".ifs/templates/scripts/Invoke-IfsAppDeploy.ps1",
            ["scripts.Publish-IfsImage.ps1"] = ".ifs/templates/scripts/Publish-IfsImage.ps1",
            ["scripts.Set-IfsRunName.ps1"] = ".ifs/templates/scripts/Set-IfsRunName.ps1",
            ["scripts.Test-IfsBicep.ps1"] = ".ifs/templates/scripts/Test-IfsBicep.ps1",
            ["steps.DependencyCache.yml"] = ".ifs/templates/steps/DependencyCache.yml",
            ["steps.ImageScan.yml"] = ".ifs/templates/steps/ImageScan.yml"
        };

    public static IReadOnlyList<InfraFlowSculptor.Application.Generation.GeneratedFile> ReadAll(
        string projectCode,
        string projectName) =>
        PathsByResource.OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => new InfraFlowSculptor.Application.Generation.GeneratedFile(
                entry.Value,
                Render(Read(entry.Key), projectCode, projectName)))
            .ToArray();

    public static bool ContainsTemplate(string path) =>
        PathsByResource.Values.Contains(path, StringComparer.Ordinal);

    private static string Render(string content, string projectCode, string projectName) =>
        content.Replace(ReferenceProjectCode, projectCode, StringComparison.Ordinal)
            .Replace("{{PROJECT_CODE}}", projectCode, StringComparison.Ordinal)
            .Replace("{{PROJECT_NAME}}", projectName, StringComparison.Ordinal);

    private static string Read(string resourceName)
    {
        using var stream = typeof(AzureDevOpsTemplateResources).Assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException($"The embedded Azure DevOps template '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
