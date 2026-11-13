using InfraFlowSculptor.Application.Generation;

namespace InfraFlowSculptor.Emitters.InstallKit;

internal static class ReferenceInstallResources
{
    private const string ResourcePrefix = "InfraFlowSculptor.Emitters.InstallKit.Resources.";
    private const string ReferenceProjectCode = "shop";

    private static readonly IReadOnlyDictionary<string, string> PathsByResource =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pins.json"] = ".ifs/pins.json",
            ["azure-setup.ps1"] = ".ifs/install/azure-setup.ps1",
            ["readiness.bicep"] = ".ifs/install/readiness.bicep",
            ["SETUP.md"] = ".ifs/install/SETUP.md",
            ["scripts.IfsInstall.psm1"] = ".ifs/install/scripts/IfsInstall.psm1",
            ["scripts.Install-Azdo.ps1"] = ".ifs/install/scripts/Install-Azdo.ps1"
        };

    public static IReadOnlyList<GeneratedFile> ReadAll(string projectCode, string projectName) =>
        PathsByResource.OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => new GeneratedFile(entry.Value, Render(Read(entry.Key), projectCode, projectName)))
            .ToArray();

    public static string Read(string name, string projectCode, string projectName) =>
        Render(Read(name), projectCode, projectName);

    private static string Read(string name)
    {
        using var stream = typeof(ReferenceInstallResources).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"The embedded install resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Render(string content, string projectCode, string projectName) =>
        content.Replace(ReferenceProjectCode, projectCode, StringComparison.Ordinal)
            .Replace("{{PROJECT_CODE}}", projectCode, StringComparison.Ordinal)
            .Replace("{{PROJECT_NAME}}", projectName, StringComparison.Ordinal);
}
