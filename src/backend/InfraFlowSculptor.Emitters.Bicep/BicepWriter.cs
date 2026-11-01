using System.Text;
using System.Text.Json;

namespace InfraFlowSculptor.Emitters.Bicep;

internal sealed class BicepWriter
{
    private readonly StringBuilder content = new();
    private int indentation;

    public void Line(string? value = null)
    {
        if (value is not null)
        {
            content.Append(' ', indentation * 2).Append(value);
        }

        content.Append('\n');
    }

    public void BeginBlock(string declaration)
    {
        Line(declaration);
        indentation++;
    }

    public void BeginArray(string declaration)
    {
        Line(declaration);
        indentation++;
    }

    public void EndBlock(string suffix = "")
    {
        indentation--;
        Line($"}}{suffix}");
    }

    public void EndArray()
    {
        indentation--;
        Line("]");
    }

    public void BlankLine() => Line();

    public override string ToString() => content.ToString();

    public static string StringLiteral(string value) => JsonSerializer.Serialize(value);
}
