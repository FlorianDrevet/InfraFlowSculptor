using System.Text;

namespace InfraFlowSculptor.Emitters.AzureDevOps;

internal sealed class YamlWriter
{
    private readonly StringBuilder content = new();

    public void WriteLine(string line = "") => content.Append(line).Append('\n');

    public string Build() => content.ToString();
}
