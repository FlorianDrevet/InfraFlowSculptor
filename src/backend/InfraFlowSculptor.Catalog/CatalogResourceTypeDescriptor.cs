using System.Text.Json;

namespace InfraFlowSculptor.Catalog;

public sealed class CatalogResourceTypeDescriptor
{
    internal CatalogResourceTypeDescriptor(JsonElement definition)
    {
        Definition = definition.Clone();
        Type = Definition.GetProperty("type").GetString()!;
        Category = Definition.GetProperty("category").GetString()!;
        Icon = Definition.GetProperty("icon").GetString()!;
        var labels = Definition.GetProperty("labels");
        LabelFr = labels.GetProperty("fr").GetString()!;
        LabelEn = labels.GetProperty("en").GetString()!;
    }

    public string Type { get; }

    public string Category { get; }

    public string Icon { get; }

    public string LabelFr { get; }

    public string LabelEn { get; }

    public JsonElement Definition { get; }
}
