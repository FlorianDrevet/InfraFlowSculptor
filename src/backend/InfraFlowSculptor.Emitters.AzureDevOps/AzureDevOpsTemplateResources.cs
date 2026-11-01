namespace InfraFlowSculptor.Emitters.AzureDevOps;

internal static class AzureDevOpsTemplateResources
{
    private const string ResourcePrefix = "InfraFlowSculptor.Emitters.AzureDevOps.Templates.";

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
            ["scripts.IfsRelease.psm1"] = ".ifs/templates/scripts/IfsRelease.psm1",
            ["scripts.Invoke-IfsInfraPreview.ps1"] = ".ifs/templates/scripts/Invoke-IfsInfraPreview.ps1",
            ["scripts.Invoke-IfsInfraDeploy.ps1"] = ".ifs/templates/scripts/Invoke-IfsInfraDeploy.ps1",
            ["scripts.Invoke-IfsAppDeploy.ps1"] = ".ifs/templates/scripts/Invoke-IfsAppDeploy.ps1"
        };

    public static IReadOnlyList<InfraFlowSculptor.Application.Generation.GeneratedFile> ReadAll() =>
        PathsByResource.OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => new InfraFlowSculptor.Application.Generation.GeneratedFile(
                entry.Value,
                Read(entry.Key)))
            .ToArray();

    private static string Read(string resourceName)
    {
        using var stream = typeof(AzureDevOpsTemplateResources).Assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException($"The embedded Azure DevOps template '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
